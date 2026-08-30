using System.Globalization;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Maps <see cref="VideoPlayerSettingsDto"/> subtitle appearance fields to CSS values
/// shared by the settings preview and the video player.
/// </summary>
public static class SubtitleStyleHelper
{
    public sealed record CssStyle(
        string FontFamily,
        string FontSize,
        string Color,
        string BackgroundColor,
        string TextShadow);

    public static CssStyle ToCss(VideoPlayerSettingsDto settings, DeviceType deviceType = DeviceType.Desktop)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalizedDevice = NormalizeDeviceType(deviceType);

        return new CssStyle(
            ToFontFamilyCss(settings.SubtitleFontFamily),
            ToFontSizeCss(settings.SubtitleFontSize, normalizedDevice),
            NormalizeColor(settings.SubtitleFontColor),
            ToBackgroundCss(settings.SubtitleBackgroundOpacity),
            ToTextShadowCss(settings.SubtitleShadowEnabled, settings.SubtitleShadowColor, settings.SubtitleShadowBlur));
    }

    public static DeviceType NormalizeDeviceType(DeviceType deviceType) => deviceType switch
    {
        DeviceType.Phone or DeviceType.Watch => DeviceType.Phone,
        DeviceType.Unknown => DeviceType.Desktop,
        _ => deviceType
    };

    public static int ToFontSizePx(SubtitleFontSize size, DeviceType deviceType = DeviceType.Desktop) =>
        (size, NormalizeDeviceType(deviceType)) switch
        {
            (SubtitleFontSize.Small, DeviceType.Phone) => 12,
            (SubtitleFontSize.Small, DeviceType.Tablet) => 14,
            (SubtitleFontSize.Small, DeviceType.TV) => 28,
            (SubtitleFontSize.Small, _) => 16,

            (SubtitleFontSize.Large, DeviceType.Phone) => 20,
            (SubtitleFontSize.Large, DeviceType.Tablet) => 24,
            (SubtitleFontSize.Large, DeviceType.TV) => 60,
            (SubtitleFontSize.Large, _) => 32,

            (SubtitleFontSize.Medium, DeviceType.Phone) => 16,
            (SubtitleFontSize.Medium, DeviceType.Tablet) => 18,
            (SubtitleFontSize.Medium, DeviceType.TV) => 40,
            _ => 22
        };

    public static string ToFontFamilyCss(SubtitleFontFamily family) => family switch
    {
        SubtitleFontFamily.Manrope => "'Manrope', sans-serif",
        SubtitleFontFamily.Epilogue => "'Epilogue', sans-serif",
        SubtitleFontFamily.SansSerif => "sans-serif",
        SubtitleFontFamily.Serif => "serif",
        SubtitleFontFamily.Monospace => "monospace",
        _ => "inherit"
    };

    public static string ToFontSizeCss(SubtitleFontSize size, DeviceType deviceType = DeviceType.Desktop) =>
        $"{ToFontSizePx(size, deviceType).ToString(CultureInfo.InvariantCulture)}px";

    public static float ToFontSizeSp(SubtitleFontSize size, DeviceType deviceType = DeviceType.Desktop) =>
        ToFontSizePx(size, deviceType);

    public static string ToBackgroundCss(double opacity)
    {
        var clamped = Math.Clamp(opacity, 0, 1);
        return $"rgba(0, 0, 0, {clamped.ToString(CultureInfo.InvariantCulture)})";
    }

    public static string ToTextShadowCss(bool enabled, string shadowColor, double blur)
    {
        if (!enabled)
            return "none";

        var color = NormalizeColor(shadowColor);
        var blurPx = Math.Max(0, blur).ToString(CultureInfo.InvariantCulture);
        return $"0 0 {blurPx}px {color}, 1px 1px {blurPx}px {color}";
    }

    public static string NormalizeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return "#FFFFFF";

        var trimmed = color.Trim();
        if (trimmed.StartsWith('#') && (trimmed.Length is 4 or 7 or 9))
            return trimmed;

        return "#FFFFFF";
    }

    /// <summary>Parses #RGB / #RRGGBB / #AARRGGBB into ARGB components (A defaults to 255).</summary>
    public static bool TryParseHexColor(string? color, out byte a, out byte r, out byte g, out byte b)
    {
        a = 255;
        r = g = b = 255;

        var hex = NormalizeColor(color).TrimStart('#');
        try
        {
            switch (hex.Length)
            {
                case 3:
                    r = Convert.ToByte(new string(hex[0], 2), 16);
                    g = Convert.ToByte(new string(hex[1], 2), 16);
                    b = Convert.ToByte(new string(hex[2], 2), 16);
                    return true;
                case 6:
                    r = Convert.ToByte(hex[..2], 16);
                    g = Convert.ToByte(hex[2..4], 16);
                    b = Convert.ToByte(hex[4..6], 16);
                    return true;
                case 8:
                    a = Convert.ToByte(hex[..2], 16);
                    r = Convert.ToByte(hex[2..4], 16);
                    g = Convert.ToByte(hex[4..6], 16);
                    b = Convert.ToByte(hex[6..8], 16);
                    return true;
                default:
                    return false;
            }
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
