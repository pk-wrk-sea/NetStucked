using System.Windows;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetStucked.Core;
using NetStucked.Infrastructure;
using NetStucked.Desktop.Services;
using NetStucked.Desktop.ViewModels;

namespace NetStucked.Desktop;

public partial class App : Application
{
    private ServiceProvider? _provider;
    private Mutex? _applicationMutex;
    private bool _ownsApplicationMutex;
    private CancellationTokenSource? _pipeStop;
    private Task? _restorePipe;
    private static string ProfileIdentity(string directory) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(System.IO.Path.GetFullPath(directory).ToUpperInvariant())))[..24];
    private async Task ListenForRestoreAsync(string name, MainWindow window, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await using var pipe = new System.IO.Pipes.NamedPipeServerStream(name, System.IO.Pipes.PipeDirection.In, 1, System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous | System.IO.Pipes.PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(token).ConfigureAwait(false); byte[] message = new byte[1];
                if (await pipe.ReadAsync(message, token).ConfigureAwait(false) == 1 && message[0] == 1) _ = Dispatcher.BeginInvoke(new Action(window.RestoreFromTray));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (System.IO.IOException) { }
    }
    private async Task RestoreExistingAsync(string name)
    {
        try { await using var pipe = new System.IO.Pipes.NamedPipeClientStream(".", name, System.IO.Pipes.PipeDirection.Out, System.IO.Pipes.PipeOptions.Asynchronous); using var timeout = new CancellationTokenSource(3000); await pipe.ConnectAsync(timeout.Token); await pipe.WriteAsync(new byte[] {1}, timeout.Token); }
        catch (Exception ex) when (ex is System.IO.IOException or OperationCanceledException or UnauthorizedAccessException) { }
        finally { Shutdown(); }
    }
    private void PowerChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        var engine = _provider?.GetService<MonitoringService>();
        if (engine is null) return;
        if (e.Mode == Microsoft.Win32.PowerModes.Suspend) engine.SetSuspended(true, "Windows sleep: monitoring observation gap");
        if (e.Mode == Microsoft.Win32.PowerModes.Resume) engine.SetSuspended(false, "Windows resumed: fresh samples required");
    }
    private void NetworkChanged(object? sender, System.Net.NetworkInformation.NetworkAvailabilityEventArgs e) => _provider?.GetService<MonitoringService>()?.SetSuspended(!e.IsAvailable, e.IsAvailable ? "Local network available: fresh samples required" : "Local network unavailable: no device outage inferred");
    public static ServiceProvider ConfigureServices(UserSettingsStore? store = null, IDesktopDialogs? dialogs = null, Action<IServiceCollection>? configure = null)
    {
        store ??= new UserSettingsStore();
        var services = new ServiceCollection();
        services.AddSingleton(store);
        services.AddSingleton<ThemeService>(); services.AddSingleton<AppearanceViewModel>();
        services.AddLogging(builder => builder.AddProvider(new LocalLoggerProvider(store.DirectoryPath)));
        services.AddSingleton<IIcmpProbe, IcmpPingProbe>();
        services.AddSingleton<IDnsResolver, DnsResolver>();
        services.AddSingleton<ITcpProbe, TcpPortProbe>();
        services.AddSingleton<IWanIdentitySource, RipeWanIdentitySource>();
        services.AddSingleton<HopDescriptionService>();
        services.AddSingleton<IReleaseSource, GitHubReleaseSource>();
        services.AddSingleton<IExternalLinks, ExternalLinks>();
        services.AddSingleton<IApplicationUpdateService, WindowsApplicationUpdateService>();
        services.AddSingleton<IDesktopDialogs>(dialogs ?? new WpfDialogService());
        services.AddSingleton<MultiTargetPingService>();
        services.AddSingleton<IDnsTestClient, DnsTestClient>();
        services.AddSingleton<IMacVendorLookup, MacVendorLookup>();
        services.AddSingleton<ISecretVault, WindowsSecretVault>();
        services.AddSingleton<IUrlReputationLookup, VirusTotalUrlLookup>();
        services.AddSingleton<ISnmpClient, SharpSnmpClient>();
        services.AddSingleton<IMonitorStore, MonitorStore>();
        services.AddSingleton<IMonitorProbe, MonitorProbe>();
        services.AddSingleton<MonitoringService>(); services.AddSingleton<MonitoringViewModel>();
        services.AddSingleton<ITerminalKnownHostStore, TerminalKnownHostStore>();
        services.AddSingleton<ITerminalHostKeyPrompt, TerminalHostKeyPrompt>();
        services.AddSingleton<ITerminalHostTrust, TerminalHostTrust>();
        services.AddSingleton<ICollectorTransport, CollectorTransport>();
        services.AddSingleton<ICollectorSessionRunner, NetmikoCollectorSession>();
        services.AddSingleton<ICollectorCaptureFactory, CollectorCaptureFactory>();
        services.AddSingleton<CollectorController>(); services.AddSingleton<ConfigCollectorViewModel>();
        services.AddSingleton<IEngineeringStore, EngineeringStore>(); services.AddSingleton<IEngineeringFiles, EngineeringFiles>();
        services.AddSingleton<IAdvisoryLookup, AdvisoryLookup>(); services.AddSingleton<EngineeringContext>();
        services.AddSingleton<InventoryViewModel>(); services.AddSingleton<TopologyViewModel>(); services.AddSingleton<AdvisoryPlannerViewModel>(); services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<DnsResolverViewModel>(); services.AddSingleton<SubnetCalculatorViewModel>();
        services.AddSingleton<MacCheckerViewModel>(); services.AddSingleton<UrlCheckerViewModel>();
        services.AddSingleton<MultiTargetPortService>();
        services.AddSingleton<PortTestViewModel>(); services.AddSingleton<UpdatesViewModel>();
        services.AddSingleton<LivePingViewModel>(); services.AddSingleton<TracerouteWorkspaceViewModel>();
        services.AddSingleton<MainViewModel>(); services.AddSingleton<MainWindow>();
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        WindowBranding.SetApplicationIdentity();
        if (e.Args.Length == 2 && e.Args[0] == "--apply-update")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = RunUpdateHelperAsync(e.Args[1]);
            return;
        }
        string dataDirectory = Environment.GetEnvironmentVariable("NETSTUCKED_QA_DATA_DIRECTORY") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetStucked");
        string identity = ProfileIdentity(dataDirectory);
        _applicationMutex = new Mutex(false, "Local\\NetStucked.Application." + identity);
        try { _ownsApplicationMutex = _applicationMutex.WaitOne(0); } catch (AbandonedMutexException) { _ownsApplicationMutex = true; }
        if (!_ownsApplicationMutex) { ShutdownMode = ShutdownMode.OnExplicitShutdown; _ = RestoreExistingAsync("NetStucked.Restore." + identity); return; }
        // The executable smoke harness opts into an isolated data directory; ordinary launches use per-user preferences.
        _provider = ConfigureServices(new UserSettingsStore(Environment.GetEnvironmentVariable("NETSTUCKED_QA_DATA_DIRECTORY")));
        _provider.GetRequiredService<ThemeService>();
        var window = _provider.GetRequiredService<MainWindow>();
        var main = _provider.GetRequiredService<MainViewModel>(); window.DataContext = main;
        main.Updates.InstallationLockChanged += locked => window.IsEnabled = !locked;
        main.Updates.HandoffReady += () => Dispatcher.BeginInvoke(new Action(window.RequestExit));
        MainWindow = window;
        window.Show();
        _pipeStop = new(); _restorePipe = ListenForRestoreAsync("NetStucked.Restore." + identity, window, _pipeStop.Token);
        window.EnableTray(Environment.GetEnvironmentVariable("NETSTUCKED_QA_DATA_DIRECTORY") is null);
        if (Environment.GetEnvironmentVariable("NETSTUCKED_QA_DATA_DIRECTORY") is null && !System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) main.Monitoring.Engine.SetSuspended(true, "Local network unavailable at startup; no device downtime inferred");
        _ = main.Monitoring.ResumeSavedAsync();
        _ = main.Engineering.InitializeAsync();
        Microsoft.Win32.SystemEvents.PowerModeChanged += PowerChanged;
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += NetworkChanged;
        var store = _provider.GetRequiredService<UserSettingsStore>();
        if (store.LoadError is not null) _provider.GetRequiredService<IDesktopDialogs>().ShowError(store.LoadError);
        if (_provider.GetService<IMonitorStore>() is MonitorStore monitoringStore && monitoringStore.LoadError is not null) main.Monitoring.Message = monitoringStore.LoadError;
        _ = main.Updates.RefreshLocalStateAsync();
    }
    private async Task RunUpdateHelperAsync(string jobPath)
    {
        string data = Environment.GetEnvironmentVariable("NETSTUCKED_QA_DATA_DIRECTORY") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetStucked");
        string identity = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(System.IO.Path.GetFullPath(data).ToUpperInvariant())))[..24];
        using var mutex = new Mutex(false, "Local\\NetStucked.Update." + identity);
        bool owns = false;
        try
        {
            try { owns = mutex.WaitOne(0); } catch (AbandonedMutexException) { owns = true; }
            if (!owns) throw new InvalidOperationException("Another NetStucked installation is already in progress.");
            var job = await UpdateFiles.ReadAsync<UpdateJob>(jobPath, 16384, CancellationToken.None);
            UpdateFiles.ValidateJob(job, jobPath, data);
            string helper = System.IO.Path.Combine(UpdateFiles.JobDirectory(data, job.Id), "helper", "NetStucked.exe");
            if (!UpdateFiles.SamePath(Environment.ProcessPath!, helper)) throw new InvalidOperationException("Installer handoff must run from its isolated helper directory.");
            var platform = new WindowsUpdatePlatform(); platform.ValidateInstallation(job);
            await UpdateFiles.WriteAsync(System.IO.Path.Combine(UpdateFiles.JobDirectory(data, job.Id), "helper-ready.json"), new HelperReady(job.Id, job.ParentId, Environment.ProcessId), CancellationToken.None);
            var result = await new UpdateRunner(platform).RunAsync(jobPath, data, CancellationToken.None);
            await UpdateFiles.WriteAsync(System.IO.Path.Combine(UpdateFiles.JobDirectory(data, job.Id), "completed.json"), result, CancellationToken.None);
            if (!result.Success) MessageBox.Show(result.Message + "\n\nSettings backups and installer logs: " + System.IO.Path.Combine(data, "updates"), "NetStucked installation", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex) { MessageBox.Show("Installation was not started: " + ex.Message, "NetStucked installation", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { if (owns) mutex.ReleaseMutex(); Shutdown(); }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        Microsoft.Win32.SystemEvents.PowerModeChanged -= PowerChanged;
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged -= NetworkChanged;
        // Window closing has already drained sessions and stopped all presentation timers.
        _provider?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _pipeStop?.Cancel(); _restorePipe?.GetAwaiter().GetResult(); _pipeStop?.Dispose();
        if (_ownsApplicationMutex) _applicationMutex?.ReleaseMutex(); _applicationMutex?.Dispose();
        base.OnExit(e);
    }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e) { (MainWindow as NetStucked.Desktop.MainWindow)?.RequestExit(); base.OnSessionEnding(e); }
}
