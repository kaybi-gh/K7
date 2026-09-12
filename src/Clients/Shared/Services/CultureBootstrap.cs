using System.Globalization;
using K7.Shared;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Services;

public static class CultureBootstrap
{
    public static async Task InitializeAsync(
        IJSRuntime js,
        IServerInfoService serverInfoService,
        NavigationManager navigation,
        CancellationToken cancellationToken = default)
    {
        string? saved = null;
        try
        {
            saved = await js.InvokeAsync<string?>("blazorCulture.getSaved", cancellationToken);
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        string language;
        if (!string.IsNullOrEmpty(saved))
        {
            language = saved;
        }
        else
        {
            var serverInfo = await serverInfoService.GetServerInfoAsync(cancellationToken);
            if (string.IsNullOrEmpty(serverInfo?.DefaultLanguage))
                return;

            language = serverInfo.DefaultLanguage;
        }

        if (!SupportedLanguages.Interface.Any(l =>
                l.Code.Equals(language, StringComparison.OrdinalIgnoreCase)))
            language = "en";

        var current = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (string.Equals(current, language, StringComparison.OrdinalIgnoreCase))
            return;

        var culture = CultureInfo.GetCultureInfo(language);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        navigation.NavigateTo(navigation.Uri, forceLoad: true);
    }
}
