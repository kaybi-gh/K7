using K7.Clients.Shared.Interfaces;
using K7.Shared.Interfaces;
using K7.Shared.Theming;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Services;

public static class ThemeBootstrap
{
    /// <summary>
    /// Learns the server default theme. A first-time visitor (nothing restored by the startup
    /// script) gets it right away; everyone else keeps their last look until
    /// <see cref="LoadUserAsync"/> has the account's preferences.
    /// </summary>
    public static async Task InitializeAsync(
        ThemeService themeService,
        IJSRuntime js,
        IServerInfoService serverInfoService,
        CancellationToken cancellationToken = default)
    {
        var serverInfo = await serverInfoService.GetServerInfoAsync(cancellationToken);
        themeService.SetServerDefaultCards(serverInfo?.DefaultCards);
        themeService.SetServerSkins(serverInfo?.ToServerSkins());
        themeService.SetServerDefault(serverInfo?.DefaultTheme, serverInfo?.ServerThemes);

        if (themeService.IsResolved)
            return;

        var savedTheme = await InvokeOrDefaultAsync<string?>(js, "K7.getSavedTheme", cancellationToken);
        if (string.IsNullOrEmpty(savedTheme) && !themeService.IsResolved)
            themeService.ApplyServerDefault();
    }

    /// <summary>Loads the appearance preferences of the signed-in user and applies them.</summary>
    public static async Task LoadUserAsync(
        ThemeService themeService,
        IJSRuntime js,
        IServerInfoService serverInfoService,
        IUserPreferencesService preferencesService,
        IDeviceService deviceService,
        CancellationToken cancellationToken = default)
    {
        var serverInfo = await serverInfoService.GetServerInfoAsync(cancellationToken);
        themeService.SetServerDefaultCards(serverInfo?.DefaultCards);
        themeService.SetServerSkins(serverInfo?.ToServerSkins());
        themeService.SetServerDefault(serverInfo?.DefaultTheme, serverInfo?.ServerThemes);

        var deviceType = await deviceService.GetDeviceTypeAsync();
        var preferences = await preferencesService.GetAppearancePreferencesAsync(cancellationToken);
        preferences = await MigrateLegacyThemeAsync(themeService, js, preferencesService, preferences, cancellationToken);

        themeService.Apply(preferences, deviceType);
    }

    public static ValueTask ApplyToDocumentAsync(IJSRuntime js, ThemeCss css) =>
        js.InvokeVoidAsync("K7.applyAppearance", css.BaseAttribute, css.Declarations, css.ThemeColor);

    /// <summary>
    /// Before preferences were stored on the server, the theme lived in this browser only.
    /// Carries that choice over to the account once.
    /// </summary>
    private static async Task<AppearancePreferencesDto> MigrateLegacyThemeAsync(
        ThemeService themeService,
        IJSRuntime js,
        IUserPreferencesService preferencesService,
        AppearancePreferencesDto preferences,
        CancellationToken cancellationToken)
    {
        var legacyTheme = await InvokeOrDefaultAsync<string?>(js, "K7.getLegacyTheme", cancellationToken);
        if (legacyTheme is null)
            return preferences;

        try
        {
            if (preferences.Default.IsDefault
                && preferences.DeviceOverrides.Count == 0
                && BuiltInThemes.Contains(legacyTheme)
                && legacyTheme != themeService.ServerDefaultThemeId)
            {
                preferences.Default.ThemeId = legacyTheme;
                await preferencesService.UpdateAppearancePreferencesAsync(preferences, cancellationToken);
            }

            await js.InvokeVoidAsync("K7.markAppearanceSynced", cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JSException or InvalidOperationException)
        {
            // Read-only accounts cannot save: they keep the local theme for this session.
        }

        return preferences;
    }

    private static async Task<T?> InvokeOrDefaultAsync<T>(IJSRuntime js, string identifier, CancellationToken cancellationToken)
    {
        try
        {
            return await js.InvokeAsync<T?>(identifier, cancellationToken);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            return default;
        }
    }
}
