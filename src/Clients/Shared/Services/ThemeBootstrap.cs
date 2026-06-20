using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services.Resources;
using K7.Shared.Interfaces;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Services;

public static class ThemeBootstrap
{
    public static async Task InitializeAsync(
        ThemeService themeService,
        IJSRuntime js,
        IServerInfoService serverInfoService,
        CancellationToken cancellationToken = default)
    {
        string? savedTheme = null;
        try
        {
            savedTheme = await js.InvokeAsync<string?>("K7.getSavedTheme", cancellationToken);
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        ThemeDefinition theme;
        if (!string.IsNullOrEmpty(savedTheme))
        {
            theme = Themes.FromCssDataAttribute(savedTheme) ?? Themes.DefaultDark;
        }
        else
        {
            var serverInfo = await serverInfoService.GetServerInfoAsync(cancellationToken);
            theme = Themes.FromCssDataAttribute(serverInfo?.DefaultTheme) ?? Themes.DefaultDark;
        }

        themeService.Theme = theme;
    }
}
