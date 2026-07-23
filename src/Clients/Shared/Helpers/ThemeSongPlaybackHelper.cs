using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using K7.Shared.Services;

namespace K7.Clients.Shared.Helpers;

public static class ThemeSongPlaybackHelper
{
    public static async Task TryStartAsync(
        Guid mediaId,
        bool hasThemeSong,
        IMediaService mediaService,
        IUserPreferencesService preferences,
        IAmbientThemeService ambientTheme,
        IAudioPlayerService audioPlayer,
        IDeviceStorageService deviceStorage,
        CancellationToken cancellationToken = default)
    {
        if (!hasThemeSong)
        {
            if (ambientTheme.CurrentMediaId is not null)
                await ambientTheme.FadeOutAsync(1.5, cancellationToken);
            return;
        }

        if (audioPlayer.PlaybackState is PlaybackState.Playing or PlaybackState.Buffering)
            return;

        if (deviceStorage.Get(PreferenceKeys.THEME_SONGS_DISABLED_ON_DEVICE) == true)
            return;

        VideoPlayerSettingsDto settings;
        try
        {
            settings = await preferences.GetEffectiveVideoPlayerSettingsAsync(cancellationToken);
        }
        catch
        {
            settings = new VideoPlayerSettingsDto();
        }

        if (!settings.PlayThemeSongs)
            return;

        var url = mediaService.GetMediaThemeSongUrl(mediaId);
        if (string.IsNullOrEmpty(url))
            return;

        // Skip download when already playing this theme (navigation within the same media tree).
        if (ambientTheme.CurrentMediaId == mediaId)
        {
            await ambientTheme.KeepOrStartAsync(mediaId, url, [], cancellationToken: cancellationToken);
            return;
        }

        if (mediaService is not K7ServerService server)
            return;

        byte[] audioBytes;
        try
        {
            audioBytes = await server.HttpClient.GetByteArrayAsync(url, cancellationToken);
        }
        catch
        {
            return;
        }

        if (audioBytes.Length == 0)
            return;

        await ambientTheme.KeepOrStartAsync(
            mediaId,
            url,
            audioBytes,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Soft leave used when leaving the media page tree. Cancelled if navigation stays
    /// on a related media route.
    /// </summary>
    public static void ScheduleLeave(IAmbientThemeService ambientTheme, Guid mediaId) =>
        ambientTheme.ScheduleLeave(mediaId);

    /// <summary>
    /// Hard interrupt (watch / trailer) with a short fade.
    /// </summary>
    public static Task InterruptAsync(IAmbientThemeService ambientTheme, CancellationToken cancellationToken = default) =>
        ambientTheme.FadeOutAsync(0.4, cancellationToken);

    public static Task StopAsync(IAmbientThemeService ambientTheme, CancellationToken cancellationToken = default) =>
        ambientTheme.StopAsync(cancellationToken);
}
