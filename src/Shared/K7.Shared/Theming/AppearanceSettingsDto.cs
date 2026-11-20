using K7.Shared.Enums;

namespace K7.Shared.Theming;

/// <summary>One set of appearance choices. Every member left at its default follows the server.</summary>
public sealed record AppearanceSettingsDto
{
    /// <summary>Built-in or custom theme id. Null follows the server default theme.</summary>
    public string? ThemeId { get; set; }

    /// <summary>Accent override (<c>#rrggbb</c>). Null keeps the accent of the theme.</summary>
    public string? AccentColor { get; set; }

    public AppearanceTextSize TextSize { get; set; }
    public AppearanceCornerStyle CornerStyle { get; set; }

    /// <summary>
    /// True once the user picked their corners: the standard corners are then a choice, not
    /// "follow the server". Any other style is a choice whatever this says.
    /// </summary>
    public bool CornerStyleOwn { get; set; }

    /// <summary>
    /// Place of the mini music player on wide screens. Phones are not concerned. Part of the
    /// music player settings: it follows the defaults of the server with them.
    /// </summary>
    public MiniPlayerPlacement MiniPlayer { get; set; }

    /// <summary>How the backdrop of media pages ends on wide screens.</summary>
    public MediaHeroStyle MediaPageHero { get; set; }

    /// <summary>
    /// How much of the screen the backdrop and the title take. The whole screen is the immersive
    /// page: smaller sizes let cast, seasons and what follows show without scrolling.
    /// </summary>
    public MediaHeroSize MediaPageHeroSize { get; set; }

    /// <summary>Shows the poster next to the title on the pages that have one (movies, series).</summary>
    public bool MediaPagePoster { get; set; }

    /// <summary>Carousels or grids for the lists of a media page.</summary>
    public MediaListStyle MediaPageLists { get; set; }

    /// <summary>Limits a grid to its first lines, with a "show more" button for the rest.</summary>
    public bool MediaPageGridMore { get; set; }

    /// <summary>
    /// Kinds of media pages with settings of their own (series that show their seasons at
    /// once, movies that keep a full-page hero). A kind that is not here follows the
    /// <c>MediaPage*</c> settings above.
    /// </summary>
    public Dictionary<MediaPageKind, MediaPageSkinDto> MediaPageOverrides { get; set; } = [];

    /// <summary>How the page of a season shows its episodes.</summary>
    public SeasonPageLayout SeasonPage { get; set; }

    public AudioPlayerBackground AudioBackground { get; set; }
    public AudioPlayerLayout AudioLayout { get; set; }
    public AudioArtworkShape AudioArtwork { get; set; }
    public AudioSeekStyle AudioSeek { get; set; }
    public PlayerAccent AudioAccent { get; set; }

    public VideoControlsLayout VideoControls { get; set; }

    /// <summary>Adds play and skip buttons in the middle of the picture.</summary>
    public bool VideoCenterControls { get; set; }

    /// <summary>Shows the poster next to the bottom controls.</summary>
    public bool VideoPoster { get; set; }

    /// <summary>Shows the logo of the title instead of its name, when it has one.</summary>
    public bool VideoLogo { get; set; }

    public PlayerAccent VideoAccent { get; set; }

    /// <summary>
    /// True once the user set up their media pages: they keep their choices, whatever they are,
    /// instead of following the defaults of the server. Same for the two players below.
    /// </summary>
    public bool MediaPagesOwn { get; set; }

    public bool AudioPlayerOwn { get; set; }

    public bool VideoPlayerOwn { get; set; }

    public bool IsDefault => IsLookDefault && IsMediaPagesDefault && IsPlayersDefault;

    /// <summary>Theme, accent, text and corners.</summary>
    public bool IsLookDefault =>
        ThemeId is null
        && AccentColor is null
        && TextSize == AppearanceTextSize.Default
        && !OwnsCorners;

    /// <summary>The user chose their corners: they do not follow the ones of the server.</summary>
    public bool OwnsCorners => CornerStyleOwn || CornerStyle != AppearanceCornerStyle.Default;

    /// <summary>Nothing set up here: the media pages follow the defaults of the server.</summary>
    public bool IsMediaPagesDefault => !OwnsMediaPages;

    public bool IsAudioPlayerDefault => !OwnsAudioPlayer;

    public bool IsVideoPlayerDefault => !OwnsVideoPlayer;

    /// <summary>
    /// The user has media page settings of their own. A set of values that is not the
    /// built-in one counts too, whatever the flag says: it is a choice.
    /// </summary>
    public bool OwnsMediaPages => MediaPagesOwn || !MediaPagesBuiltIn;

    public bool OwnsAudioPlayer => AudioPlayerOwn || !AudioPlayerBuiltIn;

    public bool OwnsVideoPlayer => VideoPlayerOwn || !VideoPlayerBuiltIn;

    private bool MediaPagesBuiltIn =>
        MediaPageHero == MediaHeroStyle.Fade
        && MediaPageHeroSize == MediaHeroSize.Full
        && !MediaPagePoster
        && MediaPageLists == MediaListStyle.Carousel
        && !MediaPageGridMore
        && MediaPageOverrides.Count == 0
        && SeasonPage == SeasonPageLayout.Auto;

    public bool IsPlayersDefault => IsAudioPlayerDefault && IsVideoPlayerDefault;

    private bool AudioPlayerBuiltIn =>
        AudioBackground == AudioPlayerBackground.Blur
        && AudioLayout == AudioPlayerLayout.Side
        && AudioArtwork == AudioArtworkShape.Square
        && AudioSeek == AudioSeekStyle.Waveform
        && AudioAccent == PlayerAccent.Auto
        && MiniPlayer == MiniPlayerPlacement.Auto;

    private bool VideoPlayerBuiltIn =>
        VideoControls == VideoControlsLayout.Inline
        && !VideoCenterControls
        && !VideoPoster
        && !VideoLogo
        && VideoAccent == PlayerAccent.Auto;

    public void ResetMediaPages()
    {
        MediaPagesOwn = false;
        MediaPageHero = MediaHeroStyle.Fade;
        MediaPageHeroSize = MediaHeroSize.Full;
        MediaPagePoster = false;
        MediaPageLists = MediaListStyle.Carousel;
        MediaPageGridMore = false;
        MediaPageOverrides.Clear();
        SeasonPage = SeasonPageLayout.Auto;
    }

    /// <summary>The settings of one kind of media page: its own, or the shared ones.</summary>
    public MediaPageSkinDto PageSkin(MediaPageKind? kind = null) =>
        kind is { } pageKind && MediaPageOverrides.TryGetValue(pageKind, out var own) ? own : SharedPageSkin();

    /// <summary>The settings shared by the media pages that have none of their own.</summary>
    public MediaPageSkinDto SharedPageSkin() => new()
    {
        Hero = MediaPageHero,
        HeroSize = MediaPageHeroSize,
        Poster = MediaPagePoster,
        Lists = MediaPageLists,
        GridMore = MediaPageGridMore
    };

    public void SetSharedPageSkin(MediaPageSkinDto skin)
    {
        MediaPageHero = skin.Hero;
        MediaPageHeroSize = skin.HeroSize;
        MediaPagePoster = skin.Poster;
        MediaPageLists = skin.Lists;
        MediaPageGridMore = skin.GridMore;
    }

    public void CopyMediaPagesFrom(AppearanceSettingsDto other)
    {
        MediaPageHero = other.MediaPageHero;
        MediaPageHeroSize = other.MediaPageHeroSize;
        MediaPagePoster = other.MediaPagePoster;
        MediaPageLists = other.MediaPageLists;
        MediaPageGridMore = other.MediaPageGridMore;
        MediaPageOverrides = other.MediaPageOverrides.ToDictionary(p => p.Key, p => p.Value with { });
        SeasonPage = other.SeasonPage;
    }

    public void CopyAudioPlayerFrom(AppearanceSettingsDto other)
    {
        AudioBackground = other.AudioBackground;
        AudioLayout = other.AudioLayout;
        AudioArtwork = other.AudioArtwork;
        AudioSeek = other.AudioSeek;
        AudioAccent = other.AudioAccent;
        MiniPlayer = other.MiniPlayer;
    }

    public void CopyVideoPlayerFrom(AppearanceSettingsDto other)
    {
        VideoControls = other.VideoControls;
        VideoCenterControls = other.VideoCenterControls;
        VideoPoster = other.VideoPoster;
        VideoLogo = other.VideoLogo;
        VideoAccent = other.VideoAccent;
    }

    /// <summary>
    /// These settings as the app shows them: the parts the user did not set up (media pages,
    /// music player, video player, each as a whole, and the corners) come from the defaults of
    /// the server. Returns this instance when there is nothing to take from the server.
    /// </summary>
    public AppearanceSettingsDto WithServerSkins(AppearanceSettingsDto? server)
    {
        if (server is null || (OwnsMediaPages && OwnsAudioPlayer && OwnsVideoPlayer && OwnsCorners))
            return this;

        var shown = Copy();
        if (!OwnsCorners)
            shown.CornerStyle = server.CornerStyle;
        if (!OwnsMediaPages)
            shown.CopyMediaPagesFrom(server);
        if (!OwnsAudioPlayer)
            shown.CopyAudioPlayerFrom(server);
        if (!OwnsVideoPlayer)
            shown.CopyVideoPlayerFrom(server);

        return shown;
    }

    /// <summary>A copy that shares nothing with this one: <c>with</c> alone would share the page overrides.</summary>
    public AppearanceSettingsDto Copy() =>
        this with { MediaPageOverrides = MediaPageOverrides.ToDictionary(p => p.Key, p => p.Value with { }) };

    public void ResetPlayers()
    {
        ResetAudioPlayer();
        ResetVideoPlayer();
    }

    public void ResetAudioPlayer()
    {
        AudioPlayerOwn = false;
        AudioBackground = AudioPlayerBackground.Blur;
        AudioLayout = AudioPlayerLayout.Side;
        AudioArtwork = AudioArtworkShape.Square;
        AudioSeek = AudioSeekStyle.Waveform;
        AudioAccent = PlayerAccent.Auto;
        MiniPlayer = MiniPlayerPlacement.Auto;
    }

    public void ResetVideoPlayer()
    {
        VideoPlayerOwn = false;
        VideoControls = VideoControlsLayout.Inline;
        VideoCenterControls = false;
        VideoPoster = false;
        VideoLogo = false;
        VideoAccent = PlayerAccent.Auto;
    }
}
