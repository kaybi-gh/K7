using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Clients.Shared.UI.Helpers;

public static class TrailerDialogHelper
{
    public const string VideoOverlayBackdropClass = "k7-dialog-backdrop--video-overlay";

    public static async Task OpenAsync(
        IReadOnlyList<TrailerDto>? trailers,
        Guid mediaId,
        string fallbackTitle,
        IDeviceService deviceService,
        IExternalLinkService externalLinks,
        IK7DialogService dialogs,
        IAmbientThemeService ambientTheme,
        IUserPreferencesService preferences,
        IK7Snackbar snackbar,
        IStringLocalizer sharedLocalizer,
        CancellationToken cancellationToken = default)
    {
        var trailer = TrailerPlaybackHelper.Pick(trailers);
        if (trailer is null)
            return;

        await ThemeSongPlaybackHelper.InterruptAsync(ambientTheme, mediaId, cancellationToken);

        var site = string.IsNullOrWhiteSpace(trailer.Site) ? "YouTube" : trailer.Site;
        var deviceType = deviceService.CachedDeviceType ?? await deviceService.GetDeviceTypeAsync();
        var openExternally = TrailerPlaybackHelper.ShouldOpenExternally(
            deviceService.GetClientType(),
            deviceType,
            site,
            await ResolveOpenTrailersExternallyAsync(preferences, cancellationToken));

        if (openExternally)
        {
            var watchUrl = TrailerPlaybackHelper.TryBuildWatchUrl(site, trailer.Key);
            if (watchUrl is not null
                && await externalLinks.OpenAsync(watchUrl, cancellationToken))
                return;

            if (deviceService.GetClientType() != ClientType.Web
                || TrailerPlaybackHelper.TryBuildEmbedUrl(site, trailer.Key) is null)
            {
                snackbar.Add(sharedLocalizer["TrailerOpenFailed"], K7Severity.Error);
                return;
            }
        }

        await ShowOverlayAsync(trailer, site, fallbackTitle, dialogs);
    }

    private static async Task<bool> ResolveOpenTrailersExternallyAsync(
        IUserPreferencesService preferences,
        CancellationToken cancellationToken)
    {
        try
        {
            var settings = await preferences.GetEffectiveVideoPlayerSettingsAsync(cancellationToken);
            return settings.OpenTrailersExternally;
        }
        catch
        {
            return new VideoPlayerSettingsDto().OpenTrailersExternally;
        }
    }

    private static Task ShowOverlayAsync(
        TrailerDto trailer,
        string site,
        string fallbackTitle,
        IK7DialogService dialogs)
    {
        var parameters = new K7DialogParameters<TrailerDialog>
        {
            { x => x.TrailerKey, trailer.Key },
            { x => x.TrailerSite, site },
            { x => x.TrailerName, trailer.Name ?? fallbackTitle }
        };
        var options = new K7DialogOptions
        {
            FullScreen = true,
            CloseOnEscapeKey = true,
            CloseButton = true,
            BackdropClass = VideoOverlayBackdropClass
        };
        return dialogs.ShowAsync<TrailerDialog>(trailer.Name ?? fallbackTitle, parameters, options);
    }
}
