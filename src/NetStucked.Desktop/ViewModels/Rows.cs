using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NetStucked.Core;

namespace NetStucked.Desktop.ViewModels;

public class TargetRow(TargetSnapshot data) : ObservableObject
{
    private TargetSnapshot _data = data;
    public TargetSnapshot Data
    {
        get => _data;
        set
        {
            var old = _data; _data = value;
            if (!EqualityComparer<int>.Default.Equals(old.Number, value.Number)) OnPropertyChanged(nameof(Number));
            if (!EqualityComparer<string>.Default.Equals(old.Host, value.Host)) OnPropertyChanged(nameof(Host));
            if (!EqualityComparer<string>.Default.Equals(old.Description, value.Description)) OnPropertyChanged(nameof(Description));
            if (!EqualityComparer<string?>.Default.Equals(old.ResolvedIp, value.ResolvedIp)) OnPropertyChanged(nameof(ResolvedIp));
            if (!EqualityComparer<string>.Default.Equals(old.Status, value.Status)) OnPropertyChanged(nameof(Status));
            if (!EqualityComparer<double?>.Default.Equals(old.Last, value.Last)) OnPropertyChanged(nameof(Last));
            if (!EqualityComparer<double?>.Default.Equals(old.Average, value.Average)) OnPropertyChanged(nameof(Average));
            if (!EqualityComparer<double?>.Default.Equals(old.Minimum, value.Minimum)) OnPropertyChanged(nameof(Minimum));
            if (!EqualityComparer<double?>.Default.Equals(old.Maximum, value.Maximum)) OnPropertyChanged(nameof(Maximum));
            if (!EqualityComparer<long>.Default.Equals(old.Sent, value.Sent)) OnPropertyChanged(nameof(Sent));
            if (!EqualityComparer<long>.Default.Equals(old.Received, value.Received)) OnPropertyChanged(nameof(Received));
            if (!EqualityComparer<long>.Default.Equals(old.Lost, value.Lost)) OnPropertyChanged(nameof(Lost));
            if (!EqualityComparer<double>.Default.Equals(old.LossPercent, value.LossPercent)) OnPropertyChanged(nameof(LossPercent));
            if (!EqualityComparer<DateTimeOffset?>.Default.Equals(old.LastPing, value.LastPing)) OnPropertyChanged(nameof(LastPing));
            if (!EqualityComparer<string?>.Default.Equals(old.Error, value.Error)) OnPropertyChanged(nameof(Error));
            if (!EqualityComparer<int?>.Default.Equals(old.Ttl, value.Ttl)) OnPropertyChanged(nameof(Ttl));
            if (!EqualityComparer<DateTimeOffset?>.Default.Equals(old.LastSuccess, value.LastSuccess)) OnPropertyChanged(nameof(LastSuccess));
            if (!EqualityComparer<DateTimeOffset?>.Default.Equals(old.ReachableSince, value.ReachableSince)) OnPropertyChanged(nameof(ReachableSince));
            if (!EqualityComparer<DateTimeOffset?>.Default.Equals(old.UnreachableSince, value.UnreachableSince)) OnPropertyChanged(nameof(UnreachableSince));
            if (!EqualityComparer<string?>.Default.Equals(old.ResultError, value.ResultError)) OnPropertyChanged(nameof(ResultError));
        }
    }
    public int Number => _data.Number;
    public string Host => _data.Host;
    public string Description => _data.Description;
    public string? ResolvedIp => _data.ResolvedIp;
    public string Status => _data.Status;
    public double? Last => _data.Last;
    public double? Average => _data.Average;
    public double? Minimum => _data.Minimum;
    public double? Maximum => _data.Maximum;
    public long Sent => _data.Sent;
    public long Received => _data.Received;
    public long Lost => _data.Lost;
    public double LossPercent => _data.LossPercent;
    public DateTimeOffset? LastPing => _data.LastPing;
    public string? Error => _data.Error;
    public int? Ttl => _data.Ttl;
    public DateTimeOffset? LastSuccess => _data.LastSuccess;
    public DateTimeOffset? ReachableSince => _data.ReachableSince;
    public DateTimeOffset? UnreachableSince => _data.UnreachableSince;
    public string? ResultError => _data.ResultError;
    public static bool SortChanged(ICollectionView view, TargetSnapshot before, TargetSnapshot after) => view.SortDescriptions.Any(s => !Equals(SortValue(before, s.PropertyName), SortValue(after, s.PropertyName)));
    private static object? SortValue(TargetSnapshot value, string key) => key.Replace("Data.", "") switch
    {
        "Number" => value.Number,
        "Host" => value.Host,
        "Description" => value.Description,
        "ResolvedIp" => value.ResolvedIp,
        "Status" => value.Status,
        "Last" => value.Last,
        "Average" => value.Average,
        "Minimum" => value.Minimum,
        "Maximum" => value.Maximum,
        "Sent" => value.Sent,
        "Received" => value.Received,
        "Lost" => value.Lost,
        "LossPercent" => value.LossPercent,
        "LastPing" => value.LastPing,
        "Error" => value.Error,
        "Ttl" => value.Ttl,
        "LastSuccess" => value.LastSuccess,
        "ReachableSince" => value.ReachableSince,
        "UnreachableSince" => value.UnreachableSince,
        "ResultError" => value.ResultError,
        _ => value
    };

}

public class HopRow(TraceHopSnapshot data, Action<int, string> onDescription) : ObservableObject
{
    private TraceHopSnapshot _data = data;
    public TraceHopSnapshot Data
    {
        get => _data;
        set
        {
            var old = _data; _data = value;
            if (!EqualityComparer<int>.Default.Equals(old.Hop, value.Hop)) OnPropertyChanged(nameof(Hop));
            if (!EqualityComparer<string?>.Default.Equals(old.Address, value.Address)) OnPropertyChanged(nameof(Address));
            if (!EqualityComparer<string?>.Default.Equals(old.Hostname, value.Hostname)) OnPropertyChanged(nameof(Hostname));
            if (!EqualityComparer<string>.Default.Equals(old.Status, value.Status)) OnPropertyChanged(nameof(Status));
            if (!EqualityComparer<double?>.Default.Equals(old.Last, value.Last)) OnPropertyChanged(nameof(Last));
            if (!EqualityComparer<double?>.Default.Equals(old.Best, value.Best)) OnPropertyChanged(nameof(Best));
            if (!EqualityComparer<double?>.Default.Equals(old.Average, value.Average)) OnPropertyChanged(nameof(Average));
            if (!EqualityComparer<double?>.Default.Equals(old.Worst, value.Worst)) OnPropertyChanged(nameof(Worst));
            if (!EqualityComparer<double?>.Default.Equals(old.Jitter, value.Jitter)) OnPropertyChanged(nameof(Jitter));
            if (!EqualityComparer<long>.Default.Equals(old.Sent, value.Sent)) OnPropertyChanged(nameof(Sent));
            if (!EqualityComparer<long>.Default.Equals(old.Received, value.Received)) OnPropertyChanged(nameof(Received));
            if (!EqualityComparer<double>.Default.Equals(old.LossPercent, value.LossPercent)) OnPropertyChanged(nameof(LossPercent));
            if (!EqualityComparer<int>.Default.Equals(old.RouteChanges, value.RouteChanges)) OnPropertyChanged(nameof(RouteChanges));
            if (!EqualityComparer<DateTimeOffset?>.Default.Equals(old.Updated, value.Updated)) OnPropertyChanged(nameof(Updated));
        }
    }
    public int Hop => _data.Hop;
    public string? Address => _data.Address;
    public string? Hostname => _data.Hostname;
    public string Status => _data.Status;
    public double? Last => _data.Last;
    public double? Best => _data.Best;
    public double? Average => _data.Average;
    public double? Worst => _data.Worst;
    public double? Jitter => _data.Jitter;
    public long Sent => _data.Sent;
    public long Received => _data.Received;
    public double LossPercent => _data.LossPercent;
    public int RouteChanges => _data.RouteChanges;
    public DateTimeOffset? Updated => _data.Updated;
    public static bool SortChanged(ICollectionView view, TraceHopSnapshot before, TraceHopSnapshot after) => view.SortDescriptions.Any(s => !Equals(SortValue(before, s.PropertyName), SortValue(after, s.PropertyName)));
    private static object? SortValue(TraceHopSnapshot value, string key) => key.Replace("Data.", "") switch
    {
        "Hop" => value.Hop,
        "Address" => value.Address,
        "Hostname" => value.Hostname,
        "Description" => value.Description,
        "Status" => value.Status,
        "Last" => value.Last,
        "Best" => value.Best,
        "Average" => value.Average,
        "Worst" => value.Worst,
        "Jitter" => value.Jitter,
        "Sent" => value.Sent,
        "Received" => value.Received,
        "LossPercent" => value.LossPercent,
        "RouteChanges" => value.RouteChanges,
        "Updated" => value.Updated,
        _ => value
    };
    private string _description = data.Description;
    public string Description { get => _description; set { if (SetProperty(ref _description, value)) onDescription(Hop, value); } }
}

public sealed record PlaceholderViewModel(string Message);

