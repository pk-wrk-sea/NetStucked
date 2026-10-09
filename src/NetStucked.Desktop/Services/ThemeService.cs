using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using NetStucked.Infrastructure;

namespace NetStucked.Desktop.Services;

public sealed class ThemeService : IDisposable
{
    private readonly UserSettingsStore _store;
    public static string CurrentTheme { get; private set; } = "Light";
    public ThemeService(UserSettingsStore store)
    {
        _store = store;
        Apply(store.Preferences.Theme);
        SystemEvents.UserPreferenceChanged += PreferenceChanged;
    }
    private void PreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_store.Preferences.Theme == "System") Application.Current?.Dispatcher.BeginInvoke(new Action(() => Apply("System")));
    }
    public void Apply(string preference)
    {
        if (preference is not ("Light" or "Dark" or "System")) preference = "Light";
        _store.Preferences.Theme = preference;
        bool dark = preference == "Dark";
        if (preference == "System")
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            dark = key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        CurrentTheme = dark ? "Dark" : "Light";
        if (Application.Current is not { } app) return;
        foreach (var (name, light, night) in Palette)
        {
            var color = (Color)ColorConverter.ConvertFromString(dark ? night : light);
            if (app.Resources[name] is SolidColorBrush { IsFrozen: false } existing) existing.Color = color;
            else app.Resources[name] = new SolidColorBrush(color);
        }
        foreach (Window window in app.Windows) ApplyWindow(window);
    }
    public static void ApplyWindow(Window window)
    {
        window.SetResourceReference(Window.BackgroundProperty, "CanvasBrush");
        window.SetResourceReference(Window.ForegroundProperty, "TextBrush");
        void SetChrome()
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
            {
                int dark = CurrentTheme == "Dark" ? 1 : 0;
                _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
            }
        }
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero) SetChrome();
        else window.SourceInitialized += (_, _) => SetChrome();
    }
    private static readonly (string Name, string Light, string Dark)[] Palette =
    [
        ("CanvasBrush", "#F5F8FC", "#101722"), ("SurfaceBrush", "#FFFFFF", "#182230"),
        ("SidebarBrush", "#FCFDFF", "#131C28"), ("InputBrush", "#FCFDFF", "#141E2B"),
        ("ControlBrush", "#F6F9FE", "#203048"), ("BorderBrush", "#DFE7F1", "#2E3D52"),
        ("BorderStrongBrush", "#C7D7EC", "#41546E"), ("AccentBrush", "#246BFD", "#75AAFF"),
        ("ActionBrush", "#246BFD", "#246BFD"), ("OnAccentBrush", "#FFFFFF", "#FFFFFF"),
        ("TextBrush", "#172B52", "#E4ECF7"), ("MutedBrush", "#567098", "#A0B1C8"),
        ("PlaceholderBrush", "#8A9CB5", "#7F94B0"), ("SelectionBrush", "#EDF5FF", "#20334E"),
        ("TableHeaderBrush", "#F5F8FD", "#141E2B"), ("AlternateRowBrush", "#FAFCFF", "#192637"),
        ("GridLineBrush", "#EAF0F7", "#27364B"), ("ScrollThumbBrush", "#CBD7E7", "#556981"),
        ("SuccessBrush", "#008454", "#56DBA4"), ("DangerBrush", "#C73542", "#FF8590"),
        ("WarningBrush", "#A76500", "#FFC16C"), ("DangerSurfaceBrush", "#FFF5F5", "#30212B"),
        ("DangerBorderBrush", "#FFB5B5", "#7F404B"), ("WarningSurfaceBrush", "#FFFAF1", "#332C22")
    ];
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    public void Dispose() => SystemEvents.UserPreferenceChanged -= PreferenceChanged;
}
