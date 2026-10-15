using NetStucked.Core;
using NetStucked.Infrastructure;
using NetStucked.Desktop.Services;

namespace NetStucked.Desktop.ViewModels;

public partial class MacCheckerViewModel : DiagnosticToolViewModel<MacVendorResult>
{
    private readonly IMacVendorLookup _client;
    public MacCheckerViewModel(IMacVendorLookup client, UserSettingsStore store, IDesktopDialogs dialogs) : base(store, dialogs) { _client = client; Concurrency = 1; }
    protected override Task<IReadOnlyList<Func<CancellationToken, Task<MacVendorResult>>>> PlanAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); int timeout = Timeout();
        var addresses = DiagnosticInput.Lines(TargetText, 128).Select(MacAddressParser.Parse).DistinctBy(a => a.Address).ToArray();
        IReadOnlyList<Func<CancellationToken, Task<MacVendorResult>>> jobs = addresses.Select(address => new Func<CancellationToken, Task<MacVendorResult>>(t => _client.LookupAsync(address, timeout, t))).ToArray();
        return Task.FromResult(jobs);
    }
    protected override string Csv() => "Time,MAC,OUI,Vendor,AddressType,Status,Source\n" + string.Join('\n', Results.Select(r => Row(r.Time.ToString("O"), r.Address, r.Oui, r.Vendor, r.AddressType, r.Status, r.Details)));
}
