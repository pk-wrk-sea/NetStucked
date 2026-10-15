using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetStucked.Core;

namespace NetStucked.Desktop.ViewModels;

public partial class UrlCheckerViewModel : ObservableObject
{
    [ObservableProperty] private string _target = "";
    [ObservableProperty] private string _message = "Configure provider accounts and API access before checking a URL.";
    public string VirusTotalStatus => "Account API key not configured";
    public string PaloAltoStatus => "Supported URL category API endpoint and credentials not configured";
    [RelayCommand] private void Check()
    {
        try
        {
            var uri = DiagnosticInput.HttpUrl(Target.Contains("://", StringComparison.Ordinal) ? Target : "https://" + Target);
            Message = $"{uri.Host}: API access is not configured. No request was sent and no verdict is available.";
        }
        catch (ArgumentException) { Message = "Syntax error: enter an HTTP(S) URL or hostname without embedded credentials."; }
    }
}
