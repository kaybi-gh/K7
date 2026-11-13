using K7.Shared.Dtos;
using K7.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Rows of one video player sub-page (<see cref="VideoSettingsSections"/>), shared by the
/// personal settings page and the admin defaults. Edits <see cref="Settings"/> in place and
/// raises <see cref="Changed"/> after each change.
/// </summary>
public partial class VideoPlayerSettingsFields
{
    private static readonly int[] InactivityPromptChoices = [2, 3, 4, 5];

    [Parameter, EditorRequired] public VideoPlayerSettingsDto? Settings { get; set; }
    [Parameter] public EventCallback Changed { get; set; }

    /// <summary>Sub-page to render: seekbar, skip, next-episode, trailers or subtitles.</summary>
    [Parameter, EditorRequired] public string Section { get; set; } = "";

    /// <summary>Localizer of the hosting page (personal and admin pages keep their own wording).</summary>
    [Parameter, EditorRequired] public IStringLocalizer Localizer { get; set; } = default!;

    /// <summary>True on the admin page: hints describe the server default instead of the user's choice.</summary>
    [Parameter] public bool ServerDefaults { get; set; }

    private string ThumbnailsHint =>
        Localizer[ServerDefaults ? "ShowSeekbarThumbnailsHintLibrary" : "ShowSeekbarThumbnailsHint"];

    private string ChapterTicksHint =>
        Localizer[ServerDefaults ? "ShowChapterTicksHintLibrary" : "ShowChapterTicksHint"];

    private IReadOnlyList<ButtonGroupOption<IntroSkipBehavior>> SkipBehaviorOptions =>
    [
        new(IntroSkipBehavior.ShowButton, Localizer["ShowButton"]),
        new(IntroSkipBehavior.AutoSkip, Localizer["AutoSkip"]),
        new(IntroSkipBehavior.Disabled, Localizer["Disabled"])
    ];

    private IReadOnlyList<ButtonGroupOption<NextEpisodeBehavior>> NextEpisodeBehaviorOptions =>
    [
        new(NextEpisodeBehavior.PlayAfterCountdown, Localizer["NextEpisodePlayAfterCountdown"]),
        new(NextEpisodeBehavior.WaitForChoice, Localizer["NextEpisodeWaitForChoice"]),
        new(NextEpisodeBehavior.ClosePlayer, Localizer["NextEpisodeClosePlayer"])
    ];

    private IReadOnlyList<ButtonGroupOption<SubtitleFontSize>> SubtitleSizeOptions =>
    [
        new(SubtitleFontSize.Small, Localizer["Small"]),
        new(SubtitleFontSize.Medium, Localizer["Medium"]),
        new(SubtitleFontSize.Large, Localizer["Large"])
    ];

    private Task NotifyChangedAsync() => Changed.InvokeAsync();

    private static string FormatOpacity(double value) => $"{value:P0}";

    private static string FormatBlur(double value) => $"{value:F1} px";

    private static string FormatSkipSeconds(int value) => $"{value}s";
}
