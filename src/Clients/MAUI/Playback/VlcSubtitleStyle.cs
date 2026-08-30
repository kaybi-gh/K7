using System.Globalization;
using K7.Clients.Shared.Helpers;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using DeviceType = K7.Server.Domain.Enums.DeviceType;

namespace K7.Clients.MAUI.Playback;

/// <summary>
/// Maps <see cref="VideoPlayerSettingsDto"/> to LibVLC freetype instance options.
/// Live text-cue restyle is the XAML sidecar; PGS ignores these options.
/// </summary>
internal static class VlcSubtitleStyle
{
    private static VideoPlayerSettingsDto? _pending;

    public static void SetSettings(VideoPlayerSettingsDto? settings)
    {
        _pending = settings;
    }

    public static VideoPlayerSettingsDto? GetSettings() => _pending;

    public static IReadOnlyList<string> ToVlcInstanceOptions(DeviceType deviceType = DeviceType.Desktop)
    {
        var settings = _pending ?? new VideoPlayerSettingsDto();
        var normalized = SubtitleStyleHelper.NormalizeDeviceType(deviceType);
        var options = new List<string>
        {
            "--freetype-rel-fontsize=" + RelFontSize(settings.SubtitleFontSize, normalized),
            "--freetype-font=" + VlcFontName(settings.SubtitleFontFamily),
            "--freetype-opacity=255",
            "--freetype-outline-opacity=0"
        };

        if (SubtitleStyleHelper.TryParseHexColor(settings.SubtitleFontColor, out _, out var r, out var g, out var b))
            options.Add("--freetype-color=" + ((r << 16) | (g << 8) | b).ToString(CultureInfo.InvariantCulture));

        var bgOpacity = (int)Math.Clamp(settings.SubtitleBackgroundOpacity * 255.0, 0, 255);
        options.Add("--freetype-background-opacity=" + bgOpacity.ToString(CultureInfo.InvariantCulture));
        options.Add("--freetype-background-color=0");

        if (settings.SubtitleShadowEnabled)
        {
            options.Add("--freetype-shadow-opacity=128");
            if (SubtitleStyleHelper.TryParseHexColor(settings.SubtitleShadowColor, out _, out var sr, out var sg, out var sb))
                options.Add("--freetype-shadow-color=" + ((sr << 16) | (sg << 8) | sb).ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            options.Add("--freetype-shadow-opacity=0");
        }

        return options;
    }

    private static int RelFontSize(SubtitleFontSize size, DeviceType deviceType) =>
        (size, deviceType) switch
        {
            (SubtitleFontSize.Small, DeviceType.TV) => 18,
            (SubtitleFontSize.Large, DeviceType.TV) => 10,
            (SubtitleFontSize.Small, _) => 20,
            (SubtitleFontSize.Large, _) => 12,
            _ => 16
        };

    private static string VlcFontName(SubtitleFontFamily family) => family switch
    {
        SubtitleFontFamily.Serif => "serif",
        SubtitleFontFamily.Monospace => "monospace",
        SubtitleFontFamily.SansSerif or SubtitleFontFamily.Manrope or SubtitleFontFamily.Epilogue => "sans-serif",
        _ => "Segoe UI"
    };
}
