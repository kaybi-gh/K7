using K7.Shared.Enums;

namespace K7.Shared.Dtos;

public sealed record VideoPlayerSettingsDto
{
    public SubtitleFontFamily SubtitleFontFamily { get; set; } = SubtitleFontFamily.Default;
    public SubtitleFontSize SubtitleFontSize { get; set; } = SubtitleFontSize.Medium;
    public string SubtitleFontColor { get; set; } = "#FFFFFF";
    public double SubtitleBackgroundOpacity { get; set; } = 0.5;
    public bool SubtitleShadowEnabled { get; set; } = true;
    public string SubtitleShadowColor { get; set; } = "#000000";
    public double SubtitleShadowBlur { get; set; } = 3;
    public bool ShowSeekbarThumbnails { get; set; } = true;
    public bool ShowChapterTicks { get; set; } = true;
    public bool PlayThemeSongs { get; set; } = true;
    public bool OpenTrailersExternally { get; set; }
    public IntroSkipBehavior IntroSkipBehavior { get; set; } = IntroSkipBehavior.ShowButton;
    public IntroSkipBehavior OutroSkipBehavior { get; set; } = IntroSkipBehavior.ShowButton;

    /// <summary>Short-press rewind duration for video (keyboard / TV remote), in seconds.</summary>
    public int SkipBackSeconds { get; set; } = 10;

    /// <summary>Short-press fast-forward duration for video (keyboard / TV remote), in seconds.</summary>
    public int SkipForwardSeconds { get; set; } = 10;

    /// <summary>
    /// When true (default), remember the last audio/subtitle choice per movie or series
    /// and reuse it on the next play.
    /// </summary>
    public bool? RememberTrackSelection { get; set; }

    /// <summary>What happens when an episode ends and a next one exists.</summary>
    public NextEpisodeBehavior NextEpisodeBehavior { get; set; } = NextEpisodeBehavior.PlayAfterCountdown;

    /// <summary>
    /// Number of episodes auto-played in a row without any user interaction before the
    /// next-episode offer asks whether the user is still watching instead of counting down.
    /// 0 disables the prompt. Only applies to <see cref="NextEpisodeBehavior.PlayAfterCountdown"/>.
    /// </summary>
    public int InactivityPromptAfterEpisodes { get; set; }
}
