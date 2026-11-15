using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;
using K7.Shared.Theming;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// The card style preferences in effect, cascaded by the main layout to every media card. A new
/// instance is cascaded when the preferences change, so cards compare it by reference. Cards
/// outside the layout get none and use the defaults.
/// </summary>
public sealed class MediaCardStyleContext(MediaCardPreferencesDto preferences)
{
    private static readonly MediaCardStyleDto _defaultStyle = new();

    public MediaCardPreferencesDto Preferences { get; } = preferences;

    /// <summary>True when a card may show details that only the detailed feeds carry.</summary>
    public bool NeedsDetailedFeed => Preferences.NeedsDetailedFeed;

    /// <summary>True when some cards add a line of details under their title (taller cards).</summary>
    public bool ShowsDetailsBelow =>
        Styles.Any(style => style.Details.Count > 0 && style.DetailsPlacement == CardDetailPlacement.Below);

    private IEnumerable<MediaCardStyleDto> Styles => Preferences.Overrides.Values.Prepend(Preferences.Default);

    /// <summary>The style of a card, from the kind of media it shows.</summary>
    public static MediaCardStyleDto Resolve(MediaCardStyleContext? context, MediaCardViewModel? model) =>
        context is null ? _defaultStyle : context.Preferences.Resolve(GroupOf(model));

    /// <summary>
    /// Movies, series or music. Null for cards that are none of these (collections, playlists,
    /// people): they follow the common style.
    /// </summary>
    public static CardMediaGroup? GroupOf(MediaCardViewModel? model) => model switch
    {
        null => null,
        { MediaType: MediaType.Movie } => CardMediaGroup.Movies,
        { MediaType: MediaType.Serie or MediaType.SerieSeason or MediaType.SerieEpisode } => CardMediaGroup.Series,
        { MediaType: MediaType.MusicAlbum or MediaType.MusicTrack or MediaType.MusicArtist } => CardMediaGroup.Music,
        { MediaType: not null } => null,
        { Kind: MediaCardKind.Serie or MediaCardKind.Season or MediaCardKind.Episode } => CardMediaGroup.Series,
        { Kind: MediaCardKind.Cover } => CardMediaGroup.Music,
        _ => null
    };

    /// <summary>
    /// The context to cascade for these preferences: the current one when nothing about cards
    /// changed (a theme change must not re-render every card). The context keeps
    /// its own copy: the settings pages edit their preferences in place.
    /// </summary>
    public static MediaCardStyleContext? Next(MediaCardStyleContext? current, MediaCardPreferencesDto? preferences)
    {
        if (preferences is null || preferences.IsDefault)
            return null;

        return current is not null && Same(current.Preferences, preferences) ? current : new(MediaCardStyles.Copy(preferences));
    }

    private static bool Same(MediaCardPreferencesDto left, MediaCardPreferencesDto right) =>
        Same(left.Default, right.Default)
        && left.Overrides.Count == right.Overrides.Count
        && left.Overrides.All(pair => right.Overrides.TryGetValue(pair.Key, out var style) && Same(pair.Value, style));

    private static bool Same(MediaCardStyleDto left, MediaCardStyleDto right) =>
        left.ShowTitle == right.ShowTitle
        && left.ShowSubtitle == right.ShowSubtitle
        && left.DetailsPlacement == right.DetailsPlacement
        && left.Hover == right.Hover
        && left.Details.SequenceEqual(right.Details);
}
