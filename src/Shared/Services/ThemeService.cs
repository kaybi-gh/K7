using MediaClient.Shared.Models;
using MediaClient.Shared.Services.Resources;

namespace MediaClient.Shared.Services;

public class ThemeService
{
    public event Action? ThemeOnChange;
    private ThemeWrapper? _themeWrapper;
    public ThemeWrapper ThemeWrapper
    {
        get => _themeWrapper ?? Themes.MyTheme;
        set
        {
            _themeWrapper = value;
            ThemeOnChange?.Invoke();
        }
    }

    public event Action? DarkModeEnabledOnChange;
    private bool? _darkModeEnabled;
    public bool DarkModeEnabled
    {
        get => _darkModeEnabled ?? false;
        set
        {
            _darkModeEnabled = value;
            DarkModeEnabledOnChange?.Invoke();
        }
    }

    public void ToggleDarkMode()
    {
        DarkModeEnabled = !DarkModeEnabled;
    }
}