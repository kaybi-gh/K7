using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services.Resources;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminGeneralPanel
{
    [Inject] private IServerInfoService ServerInfoService { get; set; } = default!;

    private string _defaultLanguage = "en";
    private ThemeDefinition _defaultTheme = Themes.DefaultDark;
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var serverInfo = await ServerInfoService.GetServerInfoAsync();
            if (serverInfo is not null)
            {
                _defaultLanguage = serverInfo.DefaultLanguage;
                _defaultTheme = Themes.FromCssDataAttribute(serverInfo.DefaultTheme) ?? Themes.DefaultDark;
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnDefaultLanguageChanged(string language)
    {
        _defaultLanguage = language;
        await ServerInfoService.UpdateDefaultLanguageAsync(language);
    }

    private async Task OnDefaultThemeChanged(ThemeDefinition theme)
    {
        _defaultTheme = theme;
        await ServerInfoService.UpdateDefaultThemeAsync(theme.CssDataAttribute);
    }
}
