using CommunityToolkit.Mvvm.ComponentModel;
using NetStucked.Desktop.Services;
using NetStucked.Infrastructure;

namespace NetStucked.Desktop.ViewModels;

public partial class AppearanceViewModel : ObservableObject
{
    private readonly ThemeService _theme;
    public string[] Themes { get; } = ["Light", "Dark", "System"];
    [ObservableProperty] private string _selectedTheme;
    public bool IsDarkMode { get => ThemeService.CurrentTheme == "Dark"; set { SelectedTheme = value ? "Dark" : "Light"; OnPropertyChanged(); } }
    public bool FollowSystem { get => SelectedTheme == "System"; set { SelectedTheme = value ? "System" : ThemeService.CurrentTheme; OnPropertyChanged(); OnPropertyChanged(nameof(IsDarkMode)); } }
    public AppearanceViewModel(ThemeService theme, UserSettingsStore store) { _theme = theme; _selectedTheme = store.Preferences.Theme; theme.Changed += () => OnPropertyChanged(nameof(IsDarkMode)); }
    partial void OnSelectedThemeChanged(string value) { _theme.Apply(value); OnPropertyChanged(nameof(IsDarkMode)); OnPropertyChanged(nameof(FollowSystem)); }
}
