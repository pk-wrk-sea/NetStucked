using System.Windows;
using System.ComponentModel;
using System.Windows.Input;
using NetStucked.Desktop.ViewModels;
using NetStucked.Desktop.Behaviors;
using System.Windows.Controls.Primitives;

namespace NetStucked.Desktop;

public partial class MainWindow : Window
{
    private bool _closedAfterCleanup;
    private bool _exitRequested, _closingInProgress;
    private Services.TrayService? _tray;
    private WindowState _beforeHide;
    public MainWindow() { InitializeComponent(); Icon = Services.BrandAssets.Icon; Services.WindowBranding.Attach(this); Services.ThemeService.ApplyWindow(this); WindowWorkArea.Attach(this); Closing += ClosingAsync; }
    private void Minimize_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void Maximize_Click(object sender, RoutedEventArgs e) { if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this); else SystemCommands.MaximizeWindow(this); }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    public void EnableTray(bool enabled)
    {
        if (enabled && _tray is null && DataContext is MainViewModel main) _tray = new(this, main.Monitoring);
        else if (!enabled) { _tray?.Dispose(); _tray = null; }
    }
    public void RequestExit() { _exitRequested = true; Close(); }
    public void RestoreFromTray() { var state = IsVisible ? WindowState : _beforeHide; Show(); ShowInTaskbar = true; WindowState = state == WindowState.Minimized ? WindowState.Normal : state; Activate(); }
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (GridTools.Ancestor<ButtonBase>(e.OriginalSource as DependencyObject) is not null) return;
        if (e.ClickCount == 2) Maximize_Click(sender, e); else if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private async void ClosingAsync(object? sender, CancelEventArgs e)
    {
        if (_closedAfterCleanup) return;
        e.Cancel = true;
        if (_tray is not null && !_exitRequested) { _beforeHide = WindowState; Hide(); ShowInTaskbar = false; return; }
        if (_closingInProgress) return;
        _closingInProgress = true;
        IsEnabled = false;
        try { if (DataContext is MainViewModel vm) await vm.ShutdownAsync(); }
        catch (Exception ex) { MessageBox.Show(this, $"Shutdown/preferences: {ex.Message}", "NetStucked", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { _tray?.Dispose(); _tray = null; _closedAfterCleanup = true; Close(); }
    }
}
