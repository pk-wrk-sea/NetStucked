using NetStucked.Core;
using NetStucked.Infrastructure;
using NetStucked.Desktop.Services;

namespace NetStucked.Desktop.ViewModels;

public partial class MacCheckerViewModel : DiagnosticToolViewModel<MacVendorResult>
{
    private readonly IMacVendorLookup _client;
    public bool HasIpMatches => Results.Any(r => r.IpAddress.Length > 0);
    public MacCheckerViewModel(IMacVendorLookup client, UserSettingsStore store, IDesktopDialogs dialogs) : base(store, dialogs) { _client = client; Concurrency = 1; Results.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasIpMatches)); }
    protected override Task<IReadOnlyList<Func<CancellationToken, Task<MacVendorResult>>>> PlanAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); int timeout = Timeout();
        var rows = MacTextParser.Parse(TargetText);
        // Parse only after Check; preserve same-line mappings and input order even for repeated MACs.
        IReadOnlyList<Func<CancellationToken, Task<MacVendorResult>>> jobs = rows.Select(row => new Func<CancellationToken, Task<MacVendorResult>>(async t =>
            (await _client.LookupAsync(row.Mac, timeout, t)) with { IpAddress = row.IpAddress, SourceLine = row.SourceLine })).ToArray();
        return Task.FromResult(jobs);
    }
    protected override string Csv() => "Line,Time,MAC,IP Address,OUI,Vendor,AddressType,Status,Source\n" + string.Join('\n', Results.Select(r => Row(r.SourceLine, r.Time.ToString("O"), r.Address, r.IpAddress, r.Oui, r.Vendor, r.AddressType, r.Status, r.Details)));
}
