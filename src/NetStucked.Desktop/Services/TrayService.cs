using System.Windows;
using NetStucked.Core;
using NetStucked.Desktop.ViewModels;
using Forms = System.Windows.Forms;

namespace NetStucked.Desktop.Services;
public sealed class TrayService : IDisposable
{
    private readonly MainWindow _window;
    private readonly MonitoringViewModel _monitoring;
    private readonly Forms.NotifyIcon _icon;
    private readonly System.Drawing.Icon _drawing;
    private bool _disposed;
    public TrayService(MainWindow window, MonitoringViewModel monitoring)
    {
        _window = window; _monitoring = monitoring;
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/NetStucked;component/Resources/Brand/NetStucked.ico"))!.Stream;
        _drawing = new System.Drawing.Icon(stream);
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Restore NetStucked", null, (_, _) => window.Dispatcher.Invoke(window.RestoreFromTray));
        menu.Items.Add("Monitoring", null, (_, _) => window.Dispatcher.Invoke(() => { window.RestoreFromTray(); if (window.DataContext is MainViewModel main) main.NavigateCommand.Execute("Monitoring"); }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit NetStucked", null, (_, _) => window.Dispatcher.Invoke(window.RequestExit));
        _icon = new Forms.NotifyIcon { Icon = _drawing, Text = "NetStucked · local Monitoring", ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => window.Dispatcher.Invoke(window.RestoreFromTray);
        _icon.BalloonTipClicked += (_, _) => window.Dispatcher.Invoke(window.RestoreFromTray);
        monitoring.LocalNotification += Notify;
    }
    private void Notify(MonitorIncident incident)
    {
        if (_disposed) return;
        string text = $"{incident.DeviceName}: {(incident.EndedAt is null ? incident.Kind + " incident" : "Recovered")}\n{incident.Details}";
        if (text.Length > 512) text = text[..512];
        _icon.ShowBalloonTip(5000, "NetStucked Monitoring", text, incident.EndedAt is null ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info);
        if (_monitoring.Sound) { if (incident.EndedAt is null) System.Media.SystemSounds.Exclamation.Play(); else System.Media.SystemSounds.Asterisk.Play(); }
    }
    public void Dispose() { if (_disposed) return; _disposed = true; _monitoring.LocalNotification -= Notify; _icon.Visible = false; _icon.ContextMenuStrip?.Dispose(); _icon.Dispose(); _drawing.Dispose(); }
}
