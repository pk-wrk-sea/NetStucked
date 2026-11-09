using System.Windows;
using Forms = System.Windows.Forms;

namespace NetStucked.Desktop.Services;
public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly System.Drawing.Icon _drawing;
    private bool _disposed;
    public TrayService(MainWindow window)
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/NetStucked;component/Resources/Brand/NetStucked.ico"))!.Stream;
        _drawing = new System.Drawing.Icon(stream);
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Restore NetStucked", null, (_, _) => window.Dispatcher.Invoke(window.RestoreFromTray));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit NetStucked", null, (_, _) => window.Dispatcher.Invoke(window.RequestExit));
        _icon = new Forms.NotifyIcon { Icon = _drawing, Text = "NetStucked", ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => window.Dispatcher.Invoke(window.RestoreFromTray);
        _icon.BalloonTipClicked += (_, _) => window.Dispatcher.Invoke(window.RestoreFromTray);
    }
    public void Dispose() { if (_disposed) return; _disposed = true; _icon.Visible = false; _icon.ContextMenuStrip?.Dispose(); _icon.Dispose(); _drawing.Dispose(); }
}
