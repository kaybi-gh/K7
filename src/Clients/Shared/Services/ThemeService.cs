using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services.Resources;

namespace K7.Clients.Shared.Services;

public class ThemeService
{
    public event Action? ThemeOnChange;

    private ThemeDefinition _theme = Themes.DefaultDark;
    public ThemeDefinition Theme
    {
        get => _theme;
        set
        {
            if (_theme != value)
            {
                _theme = value;
                ThemeOnChange?.Invoke();
            }
        }
    }

    public event Action? CustomCssOnChange;

    private string? _customCss;
    public string? CustomCss
    {
        get => _customCss;
        set
        {
            if (_customCss != value)
            {
                _customCss = value;
                CustomCssOnChange?.Invoke();
            }
        }
    }
}