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
    public IntroSkipBehavior IntroSkipBehavior { get; set; } = IntroSkipBehavior.ShowButton;
    public IntroSkipBehavior OutroSkipBehavior { get; set; } = IntroSkipBehavior.ShowButton;
}
