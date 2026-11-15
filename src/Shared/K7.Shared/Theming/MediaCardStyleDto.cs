using K7.Shared.Enums;

namespace K7.Shared.Theming;

/// <summary>How media cards look: what they say, where they say it, and how they react to hover.</summary>
public sealed record MediaCardStyleDto
{
    /// <summary>Title under the card. The artwork keeps the title as its text alternative.</summary>
    public bool ShowTitle { get; set; } = true;

    /// <summary>Second line under the card (artist, series, year).</summary>
    public bool ShowSubtitle { get; set; } = true;

    /// <summary>
    /// Extra information to show. Replace the list to change it: copies of a style share it.
    /// </summary>
    public List<CardDetailField> Details { get; set; } = [];

    public CardDetailPlacement DetailsPlacement { get; set; }

    public CardHoverStyle Hover { get; set; }

    public bool IsDefault =>
        ShowTitle
        && ShowSubtitle
        && Details.Count == 0
        && DetailsPlacement == CardDetailPlacement.Below
        && Hover == CardHoverStyle.Outline;

    /// <summary>True when the style shows something the plain feeds do not carry (only the year is always there).</summary>
    public bool NeedsDetailedFeed => Details.Any(detail => detail != CardDetailField.Year);
}

/// <summary>
/// Media card preferences of one user: a common style, and an optional style of its own for
/// movies, series or music.
/// </summary>
public sealed record MediaCardPreferencesDto
{
    public MediaCardStyleDto Default { get; set; } = new();

    public Dictionary<CardMediaGroup, MediaCardStyleDto> Overrides { get; set; } = [];

    public bool IsDefault => Default.IsDefault && Overrides.Count == 0;

    public bool NeedsDetailedFeed => Default.NeedsDetailedFeed || Overrides.Values.Any(style => style.NeedsDetailedFeed);

    public bool HasOverride(CardMediaGroup group) => Overrides.ContainsKey(group);

    /// <summary>The style of a kind of media. Null (a card that is no movie, series or music) gets the common style.</summary>
    public MediaCardStyleDto Resolve(CardMediaGroup? group) =>
        group is { } key && Overrides.TryGetValue(key, out var style) ? style : Default;
}
