using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetStucked.Core;
using NetStucked.Infrastructure;

namespace NetStucked.Desktop.ViewModels;
public partial class UrlCheckerViewModel(IUrlReputationLookup lookup, ISecretVault vault, UserSettingsStore store) : ObservableObject, IAsyncDisposable
{
    public UserSettingsStore Store { get; } = store;
    [ObservableProperty] private string _target = "";
    [ObservableProperty] private string _message = "Configure your VirusTotal key, or use the embedded Palo Alto Test A Site.";
    [ObservableProperty] private UrlReputationResult? _result;
    [ObservableProperty] private bool _isChecking;
    public string VirusTotalStatus { get { try { return vault.Read("virustotal") is null ? "Account API key not configured" : "Account key saved in Windows Credential Manager"; } catch { return "Windows protected credential could not be accessed"; } } }
    public string PaloAltoStatus => "Palo Alto Test A Site · actual provider website";
    private CancellationTokenSource? _cancel;
    public void SaveKey(string key) { key = key.Trim(); if (key.Length is < 16 or > 256 || !key.All(char.IsAsciiHexDigit)) throw new ArgumentException("Enter your account API key."); vault.Write("virustotal", key); OnPropertyChanged(nameof(VirusTotalStatus)); Message = "Key saved securely. Click Check URL to retrieve an existing report."; }
    [RelayCommand] private void RemoveKey() { _cancel?.Cancel(); vault.Delete("virustotal"); OnPropertyChanged(nameof(VirusTotalStatus)); Message = "Stored account key removed."; }
    public Uri ValidUrl() => DiagnosticInput.HttpUrl(Target.Contains("://", StringComparison.Ordinal) ? Target.Trim() : "https://" + Target.Trim());
    private bool CanCheck() => !IsChecking;
    partial void OnIsCheckingChanged(bool value) => CheckCommand.NotifyCanExecuteChanged();
    [RelayCommand(CanExecute = nameof(CanCheck))] private async Task Check()
    {
        Result = null;
        try
        {
            Uri url = ValidUrl(); string? key = vault.Read("virustotal");
            if (key is null) { Message = "VirusTotal key is not configured. No request was sent. Palo Alto Test A Site is available below."; return; }
            IsChecking = true; _cancel = new(); Message = "Querying VirusTotal existing analysis…";
            Result = await lookup.LookupAsync(url, key, _cancel.Token); Message = Result.Status + ": " + Result.Details;
        }
        catch (OperationCanceledException) { Message = "Lookup cancelled."; }
        catch (Exception ex) when (ex is ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException) { Message = ex is ArgumentException ? "Syntax/configuration error: enter an HTTP(S) URL without embedded credentials and a valid account key." : "Windows protected key could not be accessed."; }
        finally { _cancel?.Dispose(); _cancel = null; IsChecking = false; }
    }
    [RelayCommand] private void Stop() => _cancel?.Cancel();
    public async Task StopForUpdateAsync() { _cancel?.Cancel(); if (CheckCommand.ExecutionTask is { } task) await task; }
    public async ValueTask DisposeAsync() => await StopForUpdateAsync();
}
