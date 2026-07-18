using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;

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
            return;

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

        await ambientTheme.PlayAsync(url, cancellationToken: cancellationToken);
    }

    public static Task StopAsync(IAmbientThemeService ambientTheme, CancellationToken cancellationToken = default) =>
        ambientTheme.StopAsync(cancellationToken);
}
