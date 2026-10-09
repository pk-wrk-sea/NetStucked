using System.Windows;
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
        services.AddLogging(builder => builder.AddProvider(new LocalLoggerProvider(store.DirectoryPath)));
        services.AddSingleton<IIcmpProbe, IcmpPingProbe>();
        services.AddSingleton<IDnsResolver, DnsResolver>();
        services.AddSingleton<ITcpProbe, TcpPortProbe>();
        services.AddSingleton<IReleaseSource, GitHubReleaseSource>();
        services.AddSingleton<IExternalLinks, ExternalLinks>();
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
        // The executable smoke harness opts into an isolated data directory; ordinary launches use per-user preferences.
        _provider = ConfigureServices(new UserSettingsStore(Environment.GetEnvironmentVariable("NETSTUCKED_QA_DATA_DIRECTORY")));
        var window = _provider.GetRequiredService<MainWindow>();
        window.DataContext = _provider.GetRequiredService<MainViewModel>();
        MainWindow = window;
        window.Show();
        var store = _provider.GetRequiredService<UserSettingsStore>();
        if (store.LoadError is not null) _provider.GetRequiredService<IDesktopDialogs>().ShowError(store.LoadError);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        // Window closing has already drained sessions and stopped all presentation timers.
        _provider?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnExit(e);
    }
}
