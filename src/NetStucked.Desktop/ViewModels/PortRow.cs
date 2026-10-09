using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NetStucked.Core;
namespace NetStucked.Desktop.ViewModels;

public sealed class PortRow(PortSnapshot data) : ObservableObject
{
    private PortSnapshot _data = data;
    public PortSnapshot Data
    {
        get => _data;
        set
        {
            var old = _data; _data = value;
            if (!Equals(old.Number, value.Number)) OnPropertyChanged(nameof(Number));
            if (!Equals(old.Host, value.Host)) OnPropertyChanged(nameof(Host));
            if (!Equals(old.Port, value.Port)) OnPropertyChanged(nameof(Port));
            if (!Equals(old.Endpoint, value.Endpoint)) OnPropertyChanged(nameof(Endpoint));
            if (!Equals(old.Description, value.Description)) OnPropertyChanged(nameof(Description));
            if (!Equals(old.ResolvedIp, value.ResolvedIp)) OnPropertyChanged(nameof(ResolvedIp));
            if (!Equals(old.Status, value.Status)) OnPropertyChanged(nameof(Status));
            if (!Equals(old.Last, value.Last)) OnPropertyChanged(nameof(Last));
            if (!Equals(old.Average, value.Average)) OnPropertyChanged(nameof(Average));
            if (!Equals(old.Minimum, value.Minimum)) OnPropertyChanged(nameof(Minimum));
            if (!Equals(old.Maximum, value.Maximum)) OnPropertyChanged(nameof(Maximum));
            if (!Equals(old.Attempts, value.Attempts)) OnPropertyChanged(nameof(Attempts));
            if (!Equals(old.Connected, value.Connected)) OnPropertyChanged(nameof(Connected));
            if (!Equals(old.FailurePercent, value.FailurePercent)) OnPropertyChanged(nameof(FailurePercent));
            if (!Equals(old.LastTest, value.LastTest)) OnPropertyChanged(nameof(LastTest));
            if (!Equals(old.LastSuccess, value.LastSuccess)) OnPropertyChanged(nameof(LastSuccess));
            if (!Equals(old.Details, value.Details)) OnPropertyChanged(nameof(Details));
        }
    }
    public int Number => _data.Number;
    public string Host => _data.Host;
    public int Port => _data.Port;
    public PortProtocol Protocol => _data.Protocol;
    public string Endpoint => _data.Endpoint;
    public string Description => _data.Description;
    public string? ResolvedIp => _data.ResolvedIp;
    public string Status => _data.Status;
    public double? Last => _data.Last;
    public double? Average => _data.Average;
    public double? Minimum => _data.Minimum;
    public double? Maximum => _data.Maximum;
    public long Attempts => _data.Attempts;
    public long Connected => _data.Connected;
    public double FailurePercent => _data.FailurePercent;
    public DateTimeOffset? LastTest => _data.LastTest;
    public DateTimeOffset? LastSuccess => _data.LastSuccess;
    public string Details => _data.Details;
    public static bool SortChanged(ICollectionView view, PortSnapshot before, PortSnapshot after) => view.SortDescriptions.Any(s => !Equals(SortValue(before, s.PropertyName), SortValue(after, s.PropertyName)));
    private static object? SortValue(PortSnapshot value, string key) => key.Replace("Data.", "") switch
    {
        "Number" => value.Number,
        "Host" => value.Host,
        "Port" => value.Port,
        "Protocol" => value.Protocol,
        "Endpoint" => value.Endpoint,
        "Description" => value.Description,
        "ResolvedIp" => value.ResolvedIp,
        "Status" => value.Status,
        "Last" => value.Last,
        "Average" => value.Average,
        "Minimum" => value.Minimum,
        "Maximum" => value.Maximum,
        "Attempts" => value.Attempts,
        "Connected" => value.Connected,
        "FailurePercent" => value.FailurePercent,
        "LastTest" => value.LastTest,
        "LastSuccess" => value.LastSuccess,
        "Details" => value.Details,
        _ => null
    };
}
