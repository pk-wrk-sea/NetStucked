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
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;
    public string CurrentVersion { get; } = typeof(UpdatesViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
    public ObservableCollection<ReleaseHistoryEntry> History { get; } = [];
    [ObservableProperty] private string _statusTitle = "Check for new versions";
    [ObservableProperty] private string _statusDetails = "Check published releases on GitHub when you choose.";
    [ObservableProperty] private string _badge = "Manual check";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _lastChecked = "Not checked yet";
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private bool _showRecoveryGuide;
    [ObservableProperty] private PublishedRelease? _available;
    public string DownloadLabel => Available?.InstallerPage is not null ? "Download installer ↗" : "View release ↗";
    public string PreviousVersion => History.FirstOrDefault(h => h.Version != CurrentVersion)?.Version is string previous ? $"Previous documented version: v{previous}" : "No earlier version recorded";
    public string RecoveryInstructions => "1. Stop all diagnostic sessions and close NetStucked.\n2. Back up the NetStucked folder under %LOCALAPPDATA%, including settings.json.\n3. Open GitHub Releases and select a previous published version. Verify its publisher before installation.\n4. Uninstall the current version, then install that previous version manually.\n5. Restore a preferences backup from that version if its settings schema is incompatible.\n\nThe documented history does not prove a previous version was installed or published. No automatic backup, download, installation or rollback runs here.";
    public UpdatesViewModel(IReleaseSource source, IExternalLinks links)
    {
        _source = source; _links = links;
        using var stream = typeof(UpdatesViewModel).Assembly.GetManifestResourceStream("NetStucked.ReleaseHistory.json")!;
        foreach (var item in JsonSerializer.Deserialize<List<ReleaseHistoryEntry>>(stream) ?? []) History.Add(item);
        Notes = History.FirstOrDefault(h => h.Version == CurrentVersion)?.Notes ?? "See the version history below.";
    }
    partial void OnIsCheckingChanged(bool value) { CheckCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(CheckLabel)); }
    partial void OnAvailableChanged(PublishedRelease? value) { OpenReleaseCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(DownloadLabel)); }
    public string CheckLabel => IsChecking ? "Checking…" : "Check for Updates";
    private bool CanCheck() => !_disposed && !IsChecking && !_lifetime.IsCancellationRequested;
    private bool CanOpenRelease() => Available is not null;
    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task CheckAsync()
    {
        IsChecking = true; Available = null;
        try
        {
            var result = await _source.CheckAsync(_lifetime.Token);
            _lifetime.Token.ThrowIfCancellationRequested();
            LastChecked = $"Last checked: {result.CheckedAt:yyyy-MM-dd HH:mm:ss zzz}";
            SemanticVersion.TryParse(CurrentVersion, out var current);
            Available = result.Releases.FirstOrDefault(r => r.Version.CompareTo(current) > 0);
            if (Available is not null)
            {
                StatusTitle = $"Version {Available.Version} available";
                StatusDetails = "Open GitHub to review and install this release manually.";
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
    private void Open(Uri page) { try { _links.OpenRelease(page); } catch (Exception ex) { StatusDetails = $"The release page could not be opened: {ex.Message}"; } }
    [RelayCommand] private void Recovery() => ShowRecoveryGuide = !ShowRecoveryGuide;
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true; CheckCommand.NotifyCanExecuteChanged();
        await _lifetime.CancelAsync();
        if (CheckCommand.ExecutionTask is { } pending) await pending;
        _lifetime.Dispose();
    }
}
