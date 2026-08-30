using K7.Shared.Dtos;

namespace K7.Clients.MAUI.Platforms.Android;

/// <summary>
/// Holds the latest <see cref="VideoPlayerSettingsDto"/> for ExoPlayer caption styling.
/// </summary>
internal static class AndroidSubtitleStyle
{
    private static VideoPlayerSettingsDto? _pending;

    public static void SetSettings(VideoPlayerSettingsDto? settings)
    {
        _pending = settings;
    }

    public static VideoPlayerSettingsDto? GetSettings() => _pending;
}
