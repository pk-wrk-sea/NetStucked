using NetStucked.Core;

namespace NetStucked.Desktop.Services;

public interface IDesktopDialogs
{
    Task<string?> LoadTargetsAsync();
    Task SaveTargetsAsync(string text);
    Task ExportAsync(string suggestedName, string csv);
    ProbeSettings? EditPingSettings(ProbeSettings settings);
    TraceSettings? EditTraceSettings(TraceSettings settings);
    PortProbeSettings? EditPortSettings(PortProbeSettings settings) => null;
    void ShowError(string message);
    string? AskTemplateName(string suggested) => null;
    string? AskPortTemplateName(string suggested) => AskTemplateName(suggested);
    bool ConfirmTemplateDeletion(string name) => false;
    HopDescriptionEdit? EditHopDescriptions(string text, bool wan) => null;
    string? EditPortTemplate(string name, string numbers) => null;
    WifiProfileInput? EditWifiProfile(WifiProfileInput input, bool allowIdentityEdit) => null;
    WifiMetadataEdit? EditWifiMetadata(string description, bool autoConnect) => null;
    EnterpriseCredentials? EditEnterpriseCredentials() => null;
    ServiceTestProfile? EditServiceTestProfile(ServiceTestProfile profile) => null;
    Task<string?> LoadWifiProfileAsync() => Task.FromResult<string?>(null);
    Task SaveWifiProfileAsync(string name, string xml) => Task.CompletedTask;
    bool ConfirmWifiDeletion(string profile) => false;
    bool ConfirmWifiImport(WifiProfile profile) => false;
}
public sealed record HopDescriptionEdit(string Text, bool WanEnabled);
public sealed record WifiMetadataEdit(string Description, bool AutoConnect);
