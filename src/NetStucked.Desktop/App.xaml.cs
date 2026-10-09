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
        services.AddSingleton<IUdpProbe, UdpPortProbe>();
        services.AddSingleton<IWifiService, WindowsWifiService>();
        services.AddSingleton<IWifiCredentialVault, WindowsWifiCredentialVault>();
        services.AddSingleton<IWifiServiceTests, WifiServiceTests>();
        services.AddSingleton<NetworkInfoViewModel>();
        services.AddSingleton<IWanIdentitySource, RipeWanIdentitySource>();
        services.AddSingleton<HopDescriptionService>();
        services.AddSingleton<IReleaseSource, GitHubReleaseSource>();
        services.AddSingleton<IExternalLinks, ExternalLinks>();
        services.AddSingleton<IApplicationUpdateService, WindowsApplicationUpdateService>();
        services.AddSingleton<IDesktopDialogs>(dialogs ?? new WpfDialogService());
        services.AddSingleton<MultiTargetPingService>();
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
        if (e.Args.Length == 2 && e.Args[0] == "--apply-update")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = RunUpdateHelperAsync(e.Args[1]);
            return;
        }
        // The executable smoke harness opts into an isolated data directory; ordinary launches use per-user preferences.
        _provider = ConfigureServices(new UserSettingsStore(Environment.GetEnvironmentVariable("NETSTUCKED_QA_DATA_DIRECTORY")));
        _provider.GetRequiredService<ThemeService>();
        var window = _provider.GetRequiredService<MainWindow>();
        var main = _provider.GetRequiredService<MainViewModel>(); window.DataContext = main;
        main.Updates.InstallationLockChanged += locked => window.IsEnabled = !locked;
        main.Updates.HandoffReady += () => Dispatcher.BeginInvoke(new Action(window.Close));
        MainWindow = window;
        window.Show();
        var store = _provider.GetRequiredService<UserSettingsStore>();
        if (store.LoadError is not null) _provider.GetRequiredService<IDesktopDialogs>().ShowError(store.LoadError);
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
        // Window closing has already drained sessions and stopped all presentation timers.
        _provider?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnExit(e);
    }
}
