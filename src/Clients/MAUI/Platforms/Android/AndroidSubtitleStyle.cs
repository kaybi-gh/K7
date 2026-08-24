using Android.Util;
using AndroidX.Media3.UI;
using K7.Clients.Shared.Helpers;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using DeviceType = K7.Server.Domain.Enums.DeviceType;
using AColor = Android.Graphics.Color;
using Typeface = Android.Graphics.Typeface;
using TypefaceStyle = Android.Graphics.TypefaceStyle;

namespace K7.Clients.MAUI.Platforms.Android;

/// <summary>
/// Applies <see cref="VideoPlayerSettingsDto"/> subtitle appearance to ExoPlayer's SubtitleView.
/// </summary>
internal static class AndroidSubtitleStyle
{
    private static VideoPlayerSettingsDto? _pending;

    public static void SetSettings(VideoPlayerSettingsDto? settings)
    {
        _pending = settings;
    }

    public static void ApplyTo(
        PlayerView? playerView,
        VideoPlayerSettingsDto? settings = null,
        DeviceType deviceType = DeviceType.Desktop)
    {
        settings ??= _pending;
        if (playerView is null || settings is null)
            return;

        var normalizedDevice = SubtitleStyleHelper.NormalizeDeviceType(deviceType);

        var subtitleView = playerView.SubtitleView;
        if (subtitleView is null)
            return;

        try
        {
            if (!SubtitleStyleHelper.TryParseHexColor(settings.SubtitleFontColor, out _, out var fr, out var fg, out var fb))
            {
                fr = fg = fb = 255;
            }

            var foreground = AColor.Rgb(fr, fg, fb);
            var alpha = (int)Math.Clamp(settings.SubtitleBackgroundOpacity * 255.0, 0, 255);
            var background = AColor.Argb(alpha, 0, 0, 0);
            var window = AColor.Argb(0, 0, 0, 0);

            if (!SubtitleStyleHelper.TryParseHexColor(settings.SubtitleShadowColor, out _, out var er, out var eg, out var eb))
            {
                er = eg = eb = 0;
            }

            var edgeColor = AColor.Rgb(er, eg, eb);
            var edgeType = settings.SubtitleShadowEnabled
                ? CaptionStyleCompat.EdgeTypeDropShadow
                : CaptionStyleCompat.EdgeTypeNone;

            var typeface = ResolveTypeface(settings.SubtitleFontFamily);
            var style = new CaptionStyleCompat(
                foreground,
                background,
                window,
                edgeType,
                edgeColor,
                typeface);

            subtitleView.SetApplyEmbeddedStyles(false);
            subtitleView.SetApplyEmbeddedFontSizes(false);
            subtitleView.SetStyle(style);
            subtitleView.SetFixedTextSize(
                (int)ComplexUnitType.Sp,
                SubtitleStyleHelper.ToFontSizeSp(settings.SubtitleFontSize, normalizedDevice));
        }
        catch
        {
            // Best-effort caption styling.
        }
    }

    private static Typeface? ResolveTypeface(SubtitleFontFamily family)
    {
        try
        {
            return family switch
            {
                SubtitleFontFamily.Serif => Typeface.Serif,
                SubtitleFontFamily.Monospace => Typeface.Monospace,
                SubtitleFontFamily.SansSerif => Typeface.SansSerif,
                SubtitleFontFamily.Manrope => Typeface.Create("sans-serif", TypefaceStyle.Normal),
                SubtitleFontFamily.Epilogue => Typeface.Create("sans-serif", TypefaceStyle.Normal),
                _ => Typeface.Default
            };
        }
        catch
        {
            return Typeface.Default;
        }
    }
}
