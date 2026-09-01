using Android.Content.PM;
using Android.Graphics;
using Android.Util;
using AndroidX.Media3.UI;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using DeviceType = K7.Server.Domain.Enums.DeviceType;
using Color = Android.Graphics.Color;
using Application = Microsoft.Maui.Controls.Application;

namespace K7.Clients.MAUI.Platforms.Android;

/// <summary>
/// Maps <see cref="VideoPlayerSettingsDto"/> onto Media3 <see cref="SubtitleView"/>
/// (ExoPlayer native text cues). Sidecar XAML cues are not used on Android Exo.
/// </summary>
internal static class AndroidExoSubtitleStyle
{
    public static void Apply(PlayerView? playerView, VideoPlayerSettingsDto? settings, DeviceType? deviceType = null)
    {
        var subtitleView = playerView?.SubtitleView;
        if (subtitleView is null)
            return;

        settings ??= new VideoPlayerSettingsDto();
        var normalized = SubtitleStyleHelper.NormalizeDeviceType(deviceType ?? ResolveDeviceType());

        try
        {
            // UX settings win over ASS/VTT embedded styles from the stream.
            subtitleView.SetApplyEmbeddedStyles(false);
            subtitleView.SetApplyEmbeddedFontSizes(false);

            // SP (not PX): helper values are CSS-px / density-independent. Physical px
            // made phone cues unreadably small on xxhdpi while TV (density ~1) looked fine.
            var sizeSp = SubtitleStyleHelper.ToFontSizeSp(settings.SubtitleFontSize, normalized);
            subtitleView.SetFixedTextSize((int)ComplexUnitType.Sp, sizeSp);

            var foreground = ParseColor(settings.SubtitleFontColor, Color.White);
            var backgroundAlpha = (int)Math.Clamp(settings.SubtitleBackgroundOpacity * 255.0, 0, 255);
            var background = Color.Argb(backgroundAlpha, 0, 0, 0);
            var edgeType = settings.SubtitleShadowEnabled
                ? CaptionStyleCompat.EdgeTypeDropShadow
                : CaptionStyleCompat.EdgeTypeNone;
            var edgeColor = settings.SubtitleShadowEnabled
                ? ParseColor(settings.SubtitleShadowColor, Color.Black)
                : Color.Transparent;

            var style = new CaptionStyleCompat(
                foreground,
                background,
                Color.Transparent,
                edgeType,
                edgeColor,
                ToTypeface(settings.SubtitleFontFamily));
            subtitleView.SetStyle(style);
        }
        catch
        {
        }
    }

    private static DeviceType ResolveDeviceType()
    {
        var cached = Application.Current?.Handler?.MauiContext?.Services
            ?.GetService<IDeviceService>()
            ?.CachedDeviceType;
        if (cached == DeviceType.TV)
            return DeviceType.TV;

        try
        {
            if (global::Android.App.Application.Context.PackageManager?
                    .HasSystemFeature(PackageManager.FeatureLeanback) == true)
                return DeviceType.TV;
        }
        catch
        {
        }

        return cached ?? DeviceType.Desktop;
    }

    private static Color ParseColor(string? hex, Color fallback)
    {
        if (!SubtitleStyleHelper.TryParseHexColor(hex, out var a, out var r, out var g, out var b))
            return fallback;
        return Color.Argb(a, r, g, b);
    }

    private static Typeface? ToTypeface(SubtitleFontFamily family) => family switch
    {
        SubtitleFontFamily.Serif => Typeface.Serif,
        SubtitleFontFamily.Monospace => Typeface.Monospace,
        SubtitleFontFamily.SansSerif or SubtitleFontFamily.Manrope or SubtitleFontFamily.Epilogue => Typeface.SansSerif,
        _ => Typeface.Default
    };
}
