using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using NetStucked.Core;

namespace NetStucked.Desktop.ViewModels;

public sealed record SubnetDetailRow(string Property, string Value);
public partial class SubnetCalculatorViewModel : ObservableObject
{
    [ObservableProperty] private string _input = "";
    [ObservableProperty] private string _message = "Input incomplete: enter an IPv4 address and /prefix or subnet mask.";
    [ObservableProperty] private bool _isInvalid;
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private string _addressNote = "";
    public ObservableCollection<SubnetDetailRow> Rows { get; } = [];
    partial void OnInputChanged(string value)
    {
        var evaluation = SubnetCalculator.Evaluate(value);
        Message = evaluation.Message; IsInvalid = evaluation.State == SubnetInputState.Invalid;
        HasResult = evaluation.Subnet is not null; Rows.Clear(); AddressNote = "";
        if (evaluation.Subnet is not { } subnet) return;
        foreach (var row in new[] {
            new SubnetDetailRow("Network / CIDR", subnet.NetworkCidr), new("Subnet mask", subnet.Mask),
            new("Wildcard mask", subnet.Wildcard), new("Broadcast", subnet.Broadcast ?? "Not applicable"),
            new("First usable", subnet.FirstUsable), new("Last usable", subnet.LastUsable),
            new("Total addresses", subnet.TotalAddresses.ToString("N0", CultureInfo.InvariantCulture)),
            new("Usable addresses", subnet.UsableAddresses.ToString("N0", CultureInfo.InvariantCulture)) }) Rows.Add(row);
        AddressNote = subnet.Prefix == 31 ? "Point-to-point /31: both addresses are usable." : subnet.Prefix == 32 ? "Host route /32: one address." : "Usable range excludes the network and broadcast addresses.";
    }
}
