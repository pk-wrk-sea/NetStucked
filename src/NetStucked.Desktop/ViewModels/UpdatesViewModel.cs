using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetStucked.Core;
using NetStucked.Desktop.Services;

namespace NetStucked.Desktop.ViewModels;

public sealed record ReleaseHistoryEntry(string Version, string Date, string Notes);
public partial class UpdatesViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IReleaseSource _source;
    private readonly IExternalLinks _links;
    private readonly IApplicationUpdateService? _updater;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _operation;
    private Task? _refreshTask;
    private bool _disposed;
    private bool _handedOff;
    public Func<Task>? StopDiagnosticsAsync { get; set; }
    public event Action<bool>? InstallationLockChanged;
    public event Action? HandoffReady;
    public string CurrentVersion { get; } = typeof(UpdatesViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
    public ObservableCollection<ReleaseHistoryEntry> History { get; } = [];
    public ObservableCollection<PublishedRelease> Builds { get; } = [];
    public ObservableCollection<SettingsBackupInfo> Backups { get; } = [];
    [ObservableProperty] private string _statusTitle = "Check for new versions";
    [ObservableProperty] private string _statusDetails = "Check published releases on GitHub when you choose.";
    [ObservableProperty] private string _badge = "Manual check";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _lastChecked = "Not checked yet";
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private bool _showRecoveryGuide;
    [ObservableProperty] private PublishedRelease? _available;
    [ObservableProperty] private PublishedRelease? _selectedBuild;
    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private bool _allowUnsignedInstaller;
    [ObservableProperty] private string _installStatus = "Choose a published build to install or recover.";
    [ObservableProperty] private double _downloadPercent;
    public bool CanChooseBuild => !IsChecking && !IsInstalling;
    public string InstallationDetails => _updater?.InstallationDetails ?? "Installer execution is unavailable in this host.";
    public string InstallLabel => SelectedBuild is null ? "Install selected build" : $"{(CompareSelected() < 0 ? "Recover" : CompareSelected() == 0 ? "Reinstall" : "Install")} v{SelectedBuild.Version}";
    public string SelectionDetails => SelectedBuild is null ? "Check GitHub to load the build list." : SelectedBuild.Installer is null ? "Installer metadata is missing or unsafe. Open the release page for manual installation." :
        $"{SelectedBuild.Installer.Bytes / 1024d / 1024d:0.0} MB • SHA-256 verification required • " + (CompareSelected() < 0 ? "Recovery installs this build and restores a matching saved settings backup when available." : "Settings are backed up before installation. Diagnostics stop and the app restarts.");
    public string BackupDetails => Backups.Count == 0 ? "No settings backups recorded yet. A verified backup is created before each installation." :
        $"{Backups.Count} verified settings backup(s). Latest: v{Backups[0].Version}, {Backups[0].CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}.";
    public string DownloadLabel => Available?.InstallerPage is not null ? "Download installer ↗" : "View release ↗";
    public string PreviousVersion => History.FirstOrDefault(h => h.Version != CurrentVersion)?.Version is string previous ? $"Previous documented version: v{previous}" : "No earlier version recorded";
    public string RecoveryInstructions => "Select an older published build above and choose Recover. NetStucked verifies the installer, stops all diagnostic sessions, backs up settings, installs the selected version and restarts. A verified settings backup for that version is restored when available; otherwise compatible schema-1 settings are retained.\n\nWindows UAC or SmartScreen may request confirmation. Recovery to a version before 0.3.0 returns to that version's manual update menu. Automatic background checking remains off.\n\nIf installation fails, inspect the installer log and preserved settings backup under %LOCALAPPDATA%\\NetStucked\\updates. The release page remains available for manual installation.";
    public UpdatesViewModel(IReleaseSource source, IExternalLinks links, IApplicationUpdateService? updater = null)
    {
        _source = source; _links = links; _updater = updater;
        using var stream = typeof(UpdatesViewModel).Assembly.GetManifestResourceStream("NetStucked.ReleaseHistory.json")!;
        foreach (var item in JsonSerializer.Deserialize<List<ReleaseHistoryEntry>>(stream) ?? []) History.Add(item);
        Notes = History.FirstOrDefault(h => h.Version == CurrentVersion)?.Notes ?? "See the version history below.";
    }
    partial void OnIsCheckingChanged(bool value) { NotifyActions(); OnPropertyChanged(nameof(CheckLabel)); }
    partial void OnAvailableChanged(PublishedRelease? value) { OpenReleaseCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(DownloadLabel)); }
    partial void OnSelectedBuildChanged(PublishedRelease? value)
    {
        if (value is not null) Notes = string.IsNullOrWhiteSpace(value.Notes) ? "No release notes supplied." : value.Notes;
        NotifyActions(); OnPropertyChanged(nameof(InstallLabel)); OnPropertyChanged(nameof(SelectionDetails)); OpenSelectedReleaseCommand.NotifyCanExecuteChanged();
    }
    partial void OnIsInstallingChanged(bool value) => NotifyActions();
    private void NotifyActions() { CheckCommand.NotifyCanExecuteChanged(); InstallCommand.NotifyCanExecuteChanged(); CancelInstallCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(CanChooseBuild)); }
    private int CompareSelected() { SemanticVersion.TryParse(CurrentVersion, out var current); return SelectedBuild?.Version.CompareTo(current) ?? 0; }
    public string CheckLabel => IsChecking ? "Checking…" : "Check for Updates";
    private bool CanCheck() => !_disposed && !_handedOff && !IsChecking && !IsInstalling && !_lifetime.IsCancellationRequested;
    private bool CanOpenRelease() => Available is not null;
    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task CheckAsync()
    {
        IsChecking = true; Available = null; SelectedBuild = null; Builds.Clear();
        try
        {
            var result = await _source.CheckAsync(_lifetime.Token);
            _lifetime.Token.ThrowIfCancellationRequested();
            LastChecked = $"Last checked: {result.CheckedAt:yyyy-MM-dd HH:mm:ss zzz}";
            SemanticVersion.TryParse(CurrentVersion, out var current);
            foreach (var build in result.Releases) Builds.Add(build);
            Available = result.Releases.FirstOrDefault(r => r.Version.CompareTo(current) > 0);
            if (Available is not null)
            {
                StatusTitle = $"Version {Available.Version} available";
                StatusDetails = "Choose a published build below to install or recover.";
                Badge = Available.Version.Major > current!.Major ? "Major update" : Available.Version.Minor > current.Minor ? "Minor update" : "Patch update";
                Notes = string.IsNullOrWhiteSpace(Available.Notes) ? "No release notes were supplied. Open the release page for details." : Available.Notes;
            }
            else
            {
                StatusTitle = result.Releases.Count == 0 ? "No published releases" : "No newer published version";
                StatusDetails = result.Releases.Count == 0 ? "GitHub has no stable SemVer release yet. You are running the current development build." : $"Latest published: v{result.Releases[0].Version}. Running: v{CurrentVersion}.";
                Badge = result.Releases.Count == 0 ? "Development build" : "Current";
                Notes = History.FirstOrDefault(h => h.Version == CurrentVersion)?.Notes ?? "See version history below.";
            }
            SelectedBuild = Available ?? Builds.FirstOrDefault(b => b.Version.CompareTo(current) == 0) ?? Builds.FirstOrDefault();
            await RefreshLocalStateAsync();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            StatusTitle = "Unable to check for updates"; Badge = "Check failed"; StatusDetails = ex is OperationCanceledException ? "The GitHub request timed out. Try again later." : ex.Message;
            LastChecked = $"Last attempt: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} • failed";
        }
        finally { IsChecking = false; }
    }
    [RelayCommand(CanExecute = nameof(CanOpenRelease))]
    private void OpenRelease() { if (Available is not null) Open(Available.Page); }
    [RelayCommand] private void OpenReleases() => Open(ReleaseRepository.ReleasesPage);
    private bool CanOpenSelectedRelease() => SelectedBuild is not null;
    [RelayCommand(CanExecute = nameof(CanOpenSelectedRelease))] private void OpenSelectedRelease() { if (SelectedBuild is not null) Open(SelectedBuild.Page); }
    private void Open(Uri page) { try { _links.OpenRelease(page); } catch (Exception ex) { StatusDetails = $"The release page could not be opened: {ex.Message}"; } }
    [RelayCommand] private void Recovery() => ShowRecoveryGuide = !ShowRecoveryGuide;
    private bool CanInstall() => !_disposed && !_handedOff && !IsChecking && !IsInstalling && _updater is not null && StopDiagnosticsAsync is not null && SelectedBuild?.Installer is not null;
    private bool CanCancelInstall() => IsInstalling && !_handedOff && _operation is not null;
    [RelayCommand(CanExecute = nameof(CanCancelInstall))] private void CancelInstall() => _operation?.Cancel();
    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        var selected = SelectedBuild;
        if (!CanInstall() || selected is null) return;
        IsInstalling = true; DownloadPercent = 0; _handedOff = false;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _operation = operation; CancelInstallCommand.NotifyCanExecuteChanged();
        StagedUpdate? staged = null;
        try
        {
            var progress = new Progress<UpdateProgress>(value =>
            {
                if (!ReferenceEquals(_operation, operation) || operation.IsCancellationRequested || _handedOff) return;
                InstallStatus = value.Stage; if (value.TotalBytes > 0) DownloadPercent = value.Percent;
            });
            staged = await _updater!.StageAsync(selected, CurrentVersion, AllowUnsignedInstaller, progress, operation.Token);
            operation.Token.ThrowIfCancellationRequested(); InstallationLockChanged?.Invoke(true);
            InstallStatus = "Stopping diagnostics and saving settings…";
            await StopDiagnosticsAsync!(); operation.Token.ThrowIfCancellationRequested();
            await _updater.StartHelperAsync(staged, operation.Token);
            _handedOff = true; CancelInstallCommand.NotifyCanExecuteChanged();
            InstallStatus = "Installer prepared. Closing NetStucked; Windows may request permission.";
            HandoffReady?.Invoke();
        }
        catch (OperationCanceledException) { InstallStatus = "Installation preparation cancelled. No installer was started."; }
        catch (Exception ex) { InstallStatus = "Unable to prepare installation: " + ex.Message; }
        finally
        {
            if (!_handedOff && staged is not null)
                try { await _updater!.DiscardAsync(staged); } catch (Exception ex) { InstallStatus += " Cache cleanup: " + ex.Message; }
            _operation = null;
            if (!_handedOff) InstallationLockChanged?.Invoke(false);
            IsInstalling = false;
        }
    }
    public Task RefreshLocalStateAsync() => _disposed ? Task.CompletedTask : _refreshTask is { IsCompleted: false } ? _refreshTask : _refreshTask = RefreshLocalStateCoreAsync();
    private async Task RefreshLocalStateCoreAsync()
    {
        if (_updater is null) return;
        try
        {
            var backups = await _updater.GetBackupsAsync(_lifetime.Token);
            Backups.Clear(); foreach (var backup in backups) Backups.Add(backup); OnPropertyChanged(nameof(BackupDetails));
            var last = await _updater.GetLastResultAsync(_lifetime.Token);
            if (last is not null) InstallStatus = $"{last.FinishedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} • {last.Message}";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { InstallStatus = "Could not read local update records: " + ex.Message; }
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true; CheckCommand.NotifyCanExecuteChanged();
        await _lifetime.CancelAsync();
        if (CheckCommand.ExecutionTask is { } pending) await pending;
        if (InstallCommand.ExecutionTask is { } installing) await installing;
        if (_refreshTask is { } refreshing) await refreshing;
        _lifetime.Dispose();
    }
}
