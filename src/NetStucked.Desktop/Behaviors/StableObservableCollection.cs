using System.Collections.ObjectModel;

namespace NetStucked.Desktop.Behaviors;

public interface IStableUpdates
{
    event EventHandler? BeforeUpdate;
    event EventHandler? AfterUpdate;
}

public sealed class StableObservableCollection<T> : ObservableCollection<T>, IStableUpdates
{
    public event EventHandler? BeforeUpdate;
    public event EventHandler? AfterUpdate;
    public void Update(Action action)
    {
        BeforeUpdate?.Invoke(this, EventArgs.Empty);
        try { action(); }
        finally { AfterUpdate?.Invoke(this, EventArgs.Empty); }
    }
}
