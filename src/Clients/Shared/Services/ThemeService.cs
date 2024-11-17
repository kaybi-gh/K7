using K7.Clients.Shared.Domain.Models;
using K7.Clients.Shared.Services.Resources;

namespace K7.Clients.Shared.Services;

public class ThemeService
{
    public event Action? ThemeOnChange;
    private ThemeWrapper? _themeWrapper;
    public ThemeWrapper ThemeWrapper
    {
        get => _themeWrapper ?? Themes.Plex;
        set
        {
            if (_themeWrapper != value)
            {
                _themeWrapper = value;
                ThemeOnChange?.Invoke();
            }
        }
    }

    public event Action? DarkModeEnabledOnChange;
    private bool? _darkModeEnabled;
    public bool DarkModeEnabled
    {
        get => _darkModeEnabled ?? true;
        set
        {
            if (_darkModeEnabled != value)
            {
                _darkModeEnabled = value;
                DarkModeEnabledOnChange?.Invoke();
            }
        }
    }

    public void ToggleDarkMode()
    {
        DarkModeEnabled = !DarkModeEnabled;
    }
}