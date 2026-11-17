using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// A small drawing of a media page or of a player with the skin settings applied, shown next
/// to those settings so a change is seen without leaving the page. It is a sketch made of
/// blocks, not the real page or player: nothing is loaded and nothing plays. It takes the shape
/// of the device the settings are for: a phone is drawn upright, with the layout phones keep.
/// </summary>
public partial class SkinPreview
{
    private const int CardCount = 7;

    // Cards on one line of a grid on the narrow screen of a phone.
    private const int PhoneGridColumns = 4;

    // Heights of the bars of the sample waveform, 0 to 1.
    private static readonly string[] WaveBars =
    [
        "0.3", "0.5", "0.8", "0.6", "0.9", "0.4", "0.7", "1", "0.6", "0.35", "0.55", "0.85",
        "0.7", "0.45", "0.9", "0.65", "0.3", "0.5", "0.75", "0.95", "0.6", "0.4", "0.7", "0.5",
        "0.85", "0.6", "0.35", "0.55", "0.8", "0.45", "0.65", "0.3"
    ];

    [Parameter, EditorRequired] public SkinPreviewKind Kind { get; set; }

    /// <summary>The settings to draw. Only the ones of <see cref="Kind"/> are read.</summary>
    [Parameter, EditorRequired] public AppearanceSettingsDto Settings { get; set; } = new();

    /// <summary>
    /// The device the settings are for. A phone gets an upright sketch of its stacked page, and
    /// the options that do not reach a device (the poster on a phone, grids on TV) are not drawn.
    /// </summary>
    [Parameter] public DeviceType Device { get; set; } = DeviceType.Unknown;

    /// <summary>The kind of media page to draw. Null draws the settings shared by all of them.</summary>
    [Parameter] public MediaPageKind? PageKind { get; set; }

    /// <summary>Text standing for the title of the movie or of the track.</summary>
    [Parameter] public string SampleTitle { get; set; } = "";

    /// <summary>What the drawing is, for assistive technologies.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    private const string PosterCard = "";
    private const string RoundCard = "sp-card--round";
    private const string WideCard = "sp-card--wide";

    private bool IsPhone => Device == DeviceType.Phone;

    private bool IsSeason => PageKind == MediaPageKind.Season;

    // The page of a season: its title then a list or a grid of episodes, unless it is the
    // immersive page with its carousel.
    private bool SeasonUnderTitle => Kind == SkinPreviewKind.MediaPage && IsSeason && !SkinClasses.SeasonImmersive(Settings, Device);

    private bool SeasonGrid => SkinClasses.SeasonLayout(Settings, Device) == SeasonPageLayout.Grid;

    // The lists each kind of page shows first: seasons for a series, the other episodes for
    // an episode, the cast for a movie, the works for a person.
    private string[] PageRows => PageKind switch
    {
        MediaPageKind.Season => [WideCard],
        MediaPageKind.Series => [PosterCard, RoundCard],
        MediaPageKind.Episode => [WideCard, RoundCard],
        MediaPageKind.Person => [PosterCard, PosterCard],
        _ => [RoundCard, PosterCard]
    };

    // The episodes of the immersive season page always are a carousel.
    private bool PageGrid => !IsSeason && SkinClasses.PageGrid(Settings, Device, PageKind);

    // No room for the poster next to the title on a phone.
    private bool PagePoster => !IsPhone && !IsSeason && PageKind != MediaPageKind.Person && SkinClasses.PagePoster(Settings, Device, PageKind);

    private string PageClass => IsSeason
        ? Join("skin-preview--page", SeasonUnderTitle ? "skin-preview--season-list" : "skin-preview--season")
        : Join(
        "skin-preview--page",
        SkinClasses.PageHero(Settings, Device, PageKind) switch
        {
            MediaHeroStyle.Cut => "skin-preview--hero-cut",
            MediaHeroStyle.Banner => "skin-preview--hero-banner",
            _ => null
        },
        SkinClasses.PageHeroSize(Settings, Device, PageKind) switch
        {
            MediaHeroSize.Half => "skin-preview--size-half",
            MediaHeroSize.Third => "skin-preview--size-third",
            _ => null
        });

    private bool PageGridMore => SkinClasses.PageGridRows(Settings, Device, PageKind) is not null;

    // A grid shows every card: three lines of them in the sketch, two when it is limited.
    private int PageCardCount => PageGrid
        ? (IsPhone ? PhoneGridColumns : CardCount) * (PageGridMore ? 2 : 3)
        : CardCount;

    // A video plays with the phone on its side: only pages and the music player stand upright.
    private string RootClass => Join(
        IsPhone && Kind != SkinPreviewKind.VideoPlayer ? "skin-preview--phone" : null,
        Kind switch
        {
            SkinPreviewKind.MediaPage => PageClass,
            SkinPreviewKind.AudioPlayer =>
                $"skin-preview--audio {SkinClasses.AudioPlayer(Settings).Replace("fullscreen-player--", "sp-audio--")}",
            _ =>
                $"skin-preview--video {SkinClasses.VideoControls(Settings).Replace("video-controls--", "sp-video--")}"
        });

    private static string Join(params string?[] classes) => string.Join(' ', classes.Where(c => c is not null));
}
