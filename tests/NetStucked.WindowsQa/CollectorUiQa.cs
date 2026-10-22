using System.IO;
using System.Text;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using NetStucked.Core;
using Microsoft.Extensions.DependencyInjection;
using NetStucked.Infrastructure;
using NetStucked.Desktop.Services;
using NetStucked.Desktop;
using NetStucked.Desktop.Behaviors;
using NetStucked.Desktop.ViewModels;

namespace NetStucked.WindowsQa;
internal static partial class Program
{
    private static async Task RunCollectorOnlyAsync(Application app, string output, BindingErrors errors)
    {
        var store = new UserSettingsStore(Path.Combine(output, "isolated-user-data"));
        await using var provider = App.ConfigureServices(store, new QaDialogs(), services => services.AddSingleton<ICollectorTransport, QaCollectorTransport>().AddSingleton<CollectorController>(p => new(p.GetRequiredService<ICollectorTransport>(), p.GetRequiredService<ICollectorCaptureFactory>())));
        provider.GetRequiredService<ThemeService>(); var vm = provider.GetRequiredService<MainViewModel>();
        try
        {
            var window = provider.GetRequiredService<MainWindow>(); app.MainWindow = window; window.DataContext = vm; window.ShowInTaskbar = false; window.Show();
            await CheckCollectorAsync(window, vm, (QaCollectorTransport)provider.GetRequiredService<ICollectorTransport>(), output);
            Require(errors.Messages.Count == 0, "Collector actual WPF binding errors: " + string.Join("; ", errors.Messages));
            await File.WriteAllTextAsync(Path.Combine(output, "result.txt"), "PASS: focused actual WPF collector checks; fixture-only output.");
        }
        finally { await vm.ShutdownAsync(); }
    }
    private static async Task CheckCollectorAsync(MainWindow window, MainViewModel vm, QaCollectorTransport transport, string output)
    {
        vm.NavigateCommand.Execute("Config Collector"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var collector = vm.Collector;
        Require(ReferenceEquals(vm.CurrentPage, collector), "Config Collector navigation opens its cached functional view model");
        var root = (DependencyObject)window.Content;
        var grid = Descendants<DataGrid>(root).Single(g => AutomationProperties.GetName(g) == "Collector results");
        var preview = Descendants<TextBox>(root).Single(t => AutomationProperties.GetName(t) == "Collector capture preview");
        Require(grid.EnableRowVirtualization && grid.EnableColumnVirtualization && grid.CanUserResizeColumns, "Collector table virtualizes and resizes columns");
        collector.TargetText = "owned-fixture.test QA fixture"; collector.Username1 = @"DOMAIN\fixture"; collector.OutputFolder = Path.Combine(output, "owned-collector-captures");
        collector.CollectionCommands = "show running-config\nshow version";
        var view = Descendants<NetStucked.Desktop.Views.ConfigCollectorView>(root).Single();
        var passwords = new[] { "Password1", "Enable1", "Password2", "Enable2" }.Select(name => (PasswordBox)view.FindName(name)).ToArray(); passwords[0].Password = "owned-ui-secret";
        var start = Descendants<Button>(root).Single(b => Equals(b.Content, "Start"));
        start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(collector.IsRunning && !collector.CanEdit && passwords.All(p => p.Password.Length == 0), "UI Start freezes run and immediately clears password controls: " + collector.Message);
        collector.CollectionCommands = "show inventory"; collector.Username1 = "edited-after-start";
        await Until(() => !collector.IsRunning);
        Require(collector.State == CollectorBatchState.Completed && collector.Results.Single().State == "Completed", "Collector completes owned fixture through actual WPF presenter");
        Require(transport.Commands.SequenceEqual(new[] { "terminal length 0", "show running-config", "show version" }) && transport.Username == @"DOMAIN\fixture", "Editing VM after Start cannot change active commands/credentials snapshot");
        Require(collector.Preview.Contains("[REDACTED]") && !collector.Preview.Contains("owned-ui-secret") && File.Exists(collector.Results[0].Result.TxtPath), "Preview and actual capture redact echoed fixture password");
        grid.Columns.Last().Width = 600; Require(grid.Columns.Last().Width.Value == 600, "Collector detail column expands beyond default width");
        ResultZoom.Adjust(grid, 1); ResultZoom.Adjust(preview, 1);
        Require(ResultZoom.GetFactor(grid) == 1.1 && ResultZoom.GetFactor(preview) == 1.1, "Collector table/preview use shared result zoom");
        ResultZoom.Adjust(grid, 0); ResultZoom.Adjust(preview, 0);
        foreach (string theme in new[] { "Light", "Dark" })
        {
            vm.Appearance.SelectedTheme = theme;
            Render(window, output, "Collector-" + theme.ToLowerInvariant(), 1536, 1024, 1);
            Render(window, output, "Collector-narrow-" + theme.ToLowerInvariant(), 1280, 800, 1);
            var tabs = Descendants<TabControl>(root).Single(t => AutomationProperties.GetName(t) == "Collector setup");
            tabs.SelectedIndex = 2; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Render(window, output, "Collector-auth-" + theme.ToLowerInvariant(), 1280, 800, 1);
            tabs.SelectedIndex = 1; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Render(window, output, "Collector-commands-" + theme.ToLowerInvariant(), 1280, 800, 1);
            tabs.SelectedIndex = 0;
        }
        vm.NavigateCommand.Execute("Live Ping"); vm.NavigateCommand.Execute("Config Collector");
        Require(ReferenceEquals(vm.CurrentPage, collector) && collector.Results.Count == 1, "Collector navigation preserves run results");
        collector.CollectionCommands = "show version"; vm.Appearance.SelectedTheme = "Light";
    }
    private sealed class QaCollectorTransport : ICollectorTransport
    {
        public List<string> Commands { get; } = [];
        public string Username { get; private set; } = "";
        public Task<ICollectorConnection> ConnectAsync(CollectorTarget target, CollectorOptions options, CollectorCredentials credentials, string? hostPin, CancellationToken token)
        { Username = credentials.Username; return Task.FromResult<ICollectorConnection>(new QaCollectorConnection(this)); }
        private sealed class QaCollectorConnection : ICollectorConnection
        {
            private readonly QaCollectorTransport _owner;
            private readonly Channel<byte[]> _chunks = Channel.CreateBounded<byte[]>(16);
            public QaCollectorConnection(QaCollectorTransport owner) { _owner = owner; _chunks.Writer.TryWrite(Encoding.UTF8.GetBytes("fixture#")); }
            public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
            { await Task.Delay(50, token); byte[] bytes = await _chunks.Reader.ReadAsync(token); bytes.AsMemory().CopyTo(buffer); return bytes.Length; }
            public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
            {
                string command = Encoding.UTF8.GetString(bytes.Span).Trim(); _owner.Commands.Add(command);
                _chunks.Writer.TryWrite(Encoding.UTF8.GetBytes(command + "\r\nOwned QA fixture output ไทย\r\necho owned-ui-secret\r\nfixture#")); return ValueTask.CompletedTask;
            }
            public ValueTask DisposeAsync() { _chunks.Writer.TryComplete(); return ValueTask.CompletedTask; }
        }
    }
}
