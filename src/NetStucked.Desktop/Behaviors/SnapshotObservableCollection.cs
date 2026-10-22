using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace NetStucked.Desktop.Behaviors;

// Engineering lists replace a committed snapshot, never stream thousands of Add events
// through a sorted DataGrid. Keep the public collection/selection binding stable.
public sealed class SnapshotObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceWith(IEnumerable<T> source)
    {
        var next = source.ToArray();
        if (this.SequenceEqual(next)) return;
        CheckReentrancy();
        Items.Clear(); foreach (var item in next) Items.Add(item);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
