using CommunityToolkit.Mvvm.ComponentModel;
using NetStucked.Desktop.Services;
using NetStucked.Infrastructure;

namespace NetStucked.Desktop.ViewModels;

public partial class AppearanceViewModel : ObservableObject
{
    private readonly ThemeService _theme;
    public string[] Themes { get; } = ["Light", "Dark", "System"];
    [ObservableProperty] private string _selectedTheme;
    public AppearanceViewModel(ThemeService theme, UserSettingsStore store) { _theme = theme; _selectedTheme = store.Preferences.Theme; }
    partial void OnSelectedThemeChanged(string value) => _theme.Apply(value);
}
