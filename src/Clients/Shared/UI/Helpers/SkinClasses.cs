using K7.Server.Domain.Enums;
using K7.Shared.Enums;
using K7.Shared.Theming;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// Turns the skin settings of an account (media pages, players) into the CSS classes that
/// carry them. Every skin is a variant of the same markup: the default adds no class.
/// </summary>
public static class SkinClasses
{
    /// <summary>Lines a grid of a media page shows before its "show more" button.</summary>
    public const int GridRowLimit = 2;

    /// <summary>
    /// Hero of media pages for a kind of page, or the shared settings. Every device follows it:
    /// wide screens and TV in the "Variants" block of <c>media-hero.css</c>, phones in their own.
    /// </summary>
    public static MediaHeroStyle PageHero(AppearanceSettingsDto settings, DeviceType deviceType, MediaPageKind? kind = null) =>
        settings.PageSkin(kind).Hero;

    /// <summary>Size of the hero of media pages for a kind of page, or the shared settings.</summary>
    public static MediaHeroSize PageHeroSize(AppearanceSettingsDto settings, DeviceType deviceType, MediaPageKind? kind = null) =>
        settings.PageSkin(kind).HeroSize;

    /// <summary>The poster next to the title of a media page. Phones hide it in CSS: no room.</summary>
    public static bool PagePoster(AppearanceSettingsDto settings, DeviceType deviceType, MediaPageKind? kind = null) =>
        settings.PageSkin(kind).Poster;

    /// <summary>
    /// Lists of media pages as grids, on every device: on TV the remote goes through a grid in
    /// reading order with left and right, and from one line to the next with up and down.
    /// </summary>
    public static bool PageGrid(AppearanceSettingsDto settings, DeviceType deviceType, MediaPageKind? kind = null) =>
        settings.PageSkin(kind).Lists == MediaListStyle.Grid;

    /// <summary>
    /// How the page of a season shows its episodes on this device. A TV always shows the
    /// carousel page, made for a remote, and is the only device to show it: the others show
    /// the list, or the grid when the setting asks for it.
    /// </summary>
    public static SeasonPageLayout SeasonLayout(AppearanceSettingsDto settings, DeviceType deviceType) =>
        deviceType == DeviceType.TV
            ? SeasonPageLayout.Carousel
            : settings.SeasonPage == SeasonPageLayout.Grid ? SeasonPageLayout.Grid : SeasonPageLayout.List;

    /// <summary>
    /// The layouts a device can show, in the order the settings offer them. A single one
    /// leaves nothing to choose.
    /// </summary>
    public static IReadOnlyList<SeasonPageLayout> SeasonLayouts(DeviceType deviceType) =>
        deviceType == DeviceType.TV
            ? [SeasonPageLayout.Carousel]
            : [SeasonPageLayout.List, SeasonPageLayout.Grid];

    /// <summary>
    /// The page of a season as the immersive page (the episode in focus on the whole screen,
    /// the episodes in a carousel) instead of a list or a grid under its title.
    /// </summary>
    public static bool SeasonImmersive(AppearanceSettingsDto settings, DeviceType deviceType) =>
        SeasonLayout(settings, deviceType) == SeasonPageLayout.Carousel;

    /// <summary>How many lines the grids of media pages show before "show more". Null shows everything.</summary>
    public static int? PageGridRows(AppearanceSettingsDto settings, DeviceType deviceType, MediaPageKind? kind = null) =>
        PageGrid(settings, deviceType, kind) && settings.PageSkin(kind).GridMore ? GridRowLimit : null;

    /// <summary>
    /// Classes of the app shell, read by the pages it holds, for the kind of media page on
    /// screen. Null when there is none.
    /// </summary>
    public static string? Shell(AppearanceSettingsDto settings, DeviceType deviceType, MediaPageKind? kind = null)
    {
        // The page of a season has no hero of this kind: nothing of it must match these classes.
        if (kind == MediaPageKind.Season)
            return null;

        var classes = Join(
            PageHero(settings, deviceType, kind) switch
            {
                MediaHeroStyle.Cut => "media-pages--hero-cut",
                MediaHeroStyle.Banner => "media-pages--hero-banner",
                _ => null
            },
            PageHeroSize(settings, deviceType, kind) switch
            {
                MediaHeroSize.Half => "media-pages--size-half",
                MediaHeroSize.Third => "media-pages--size-third",
                _ => null
            },
            PagePoster(settings, deviceType, kind) ? "media-pages--poster" : null);

        return classes.Length == 0 ? null : classes;
    }

    /// <summary>The progress shows as a ring around the cover, with the waveform or plain.</summary>
    public static bool IsRing(AppearanceSettingsDto settings) =>
        settings.AudioSeek is AudioSeekStyle.Ring or AudioSeekStyle.PlainRing;

    /// <summary>The ring goes around a disc: it makes the cover round whatever the shape setting says.</summary>
    public static bool RoundArtwork(AppearanceSettingsDto settings) =>
        settings.AudioArtwork == AudioArtworkShape.Round || IsRing(settings);

    /// <summary>Classes of the full screen music player.</summary>
    public static string AudioPlayer(AppearanceSettingsDto settings) =>
        Join(
            settings.AudioBackground switch
            {
                AudioPlayerBackground.Gradient => "fullscreen-player--bg-gradient",
                AudioPlayerBackground.Plain => "fullscreen-player--bg-plain",
                _ => null
            },
            settings.AudioLayout == AudioPlayerLayout.Centered ? "fullscreen-player--centered" : null,
            RoundArtwork(settings) ? "fullscreen-player--round" : null,
            IsRing(settings) ? "fullscreen-player--ring" : null,
            settings.AudioSeek == AudioSeekStyle.PlainRing ? "fullscreen-player--ring-plain" : null,
            settings.AudioAccent == PlayerAccent.Theme ? "fullscreen-player--accent-theme" : null);

    /// <summary>Classes of the controls of the video player.</summary>
    public static string VideoControls(AppearanceSettingsDto settings) =>
        Join(
            settings.VideoControls == VideoControlsLayout.Stacked ? "video-controls--stacked" : null,
            settings.VideoCenterControls ? "video-controls--center" : null,
            settings.VideoAccent == PlayerAccent.Theme ? "video-controls--accent-theme" : null);

    private static string Join(params string?[] classes) => string.Join(' ', classes.Where(c => c is not null));
}
