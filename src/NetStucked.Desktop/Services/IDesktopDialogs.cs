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
}
public sealed record HopDescriptionEdit(string Text, bool WanEnabled);
