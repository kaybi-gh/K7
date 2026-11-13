using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components;

/// <summary>One sub-page of a <see cref="K7SettingsSplit"/>: route key, index icon, title and short description.</summary>
public sealed record SettingsSection(string Key, string Icon, string Title, string? Description = null);

/// <summary>Sub-page keys of the video playback settings (personal page and admin defaults).</summary>
public static class VideoSettingsSections
{
    public const string Tracks = "tracks";
    public const string Seekbar = "seekbar";
    public const string Skip = "skip";
    public const string NextEpisode = "next-episode";
    public const string Trailers = "trailers";
    public const string Subtitles = "subtitles";
    public const string Resume = "resume";
    public const string Advanced = "advanced";
}

/// <summary>Sub-page keys of the audio playback settings (personal page and admin defaults).</summary>
public static class AudioSettingsSections
{
    public const string Loudness = "loudness";
    public const string Equalizer = "equalizer";
    public const string Crossfade = "crossfade";
    public const string Autoplay = "autoplay";
    public const string Quality = "quality";
    public const string Behavior = "behavior";
    public const string Tracking = "tracking";

    /// <summary>Index entries, in display order. <paramref name="localizer"/> is the audio settings page localizer.</summary>
    public static IReadOnlyList<SettingsSection> Build(IStringLocalizer localizer) =>
    [
        new(Loudness, Phosphor.Waveform, localizer["LoudnessNormalization"], localizer["LoudnessSectionDescription"]),
        new(Equalizer, Phosphor.Faders, localizer["Equalizer"], localizer["EqualizerSectionDescription"]),
        new(Crossfade, Phosphor.Intersect, localizer["Crossfade"], localizer["CrossfadeSectionDescription"]),
        new(Autoplay, Phosphor.Shuffle, localizer["Autoplay"], localizer["AutoplaySectionDescription"]),
        new(Quality, Phosphor.WifiHigh, localizer["StreamingQuality"], localizer["StreamingQualitySectionDescription"]),
        new(Behavior, Phosphor.GearSix, localizer["PlayerBehavior"], localizer["PlayerBehaviorSectionDescription"]),
        new(Tracking, Phosphor.ChartLine, localizer["TrackingSectionTitle"], localizer["TrackingSectionDescription"])
    ];
}
