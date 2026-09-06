using System.Diagnostics;
using K7.Clients.MAUI.Constants;
using K7.Clients.MAUI.Services.Authentication;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace K7.Clients.MAUI.Services;

/// <summary>
/// Server URL + session restore for both the phone window and headless car playback.
/// Android Auto binds <c>K7MediaLibraryService</c> without the phone UI window.
/// </summary>
internal static class MauiSessionBootstrap
{
    public static void ApplyServerUrl(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var k7ServerUrl = Preferences.Get(PreferenceKeys.K7_SERVER_URL, null);
        if (string.IsNullOrEmpty(k7ServerUrl))
            return;

        var manager = services.GetService<K7ServerManagerService>();
        if (manager is null)
            return;

        try
        {
            manager.UpdateBaseAddress(k7ServerUrl);
            manager.EnsureOpenIddictRegistration(k7ServerUrl);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"K7 MAUI - Session bootstrap UpdateBaseAddress failed: {ex}");
        }
    }

    public static void StartRestore(IServiceProvider? services = null)
    {
        services ??= IPlatformApplication.Current?.Services;
        if (services is null)
            return;

        ApplyServerUrl(services);

        var auth = services.GetService<AuthenticationStateProvider>();
        auth?.GetAuthenticationStateAsync().FireAndForget();
    }

    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        ApplyServerUrl(services);

        if (services.GetService<ICustomAuthenticationStateProvider>() is CustomAuthenticationStateProvider custom)
            await custom.EnsureReadyForHeadlessPlaybackAsync(cancellationToken);
        else if (services.GetService<AuthenticationStateProvider>() is { } auth)
            await auth.GetAuthenticationStateAsync();

        services.GetService<AuthSessionKeeper>()?.OnAppResumed();

        var authProvider = services.GetService<AuthenticationStateProvider>();
        if (authProvider is null)
            return;

        var userId = await AuthIdentity.GetOnlineUserIdAsync(authProvider, cancellationToken);
        if (userId is not null)
            await DeviceInitializer.InitializeDeviceAsync(services, userId);
    }
}
