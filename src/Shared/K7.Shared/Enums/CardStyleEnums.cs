namespace K7.Shared.Enums;

/// <summary>Where the extra information of a card goes. Stored in preferences: only add values at the end.</summary>
public enum CardDetailPlacement
{
    /// <summary>A line of text under the card, the default.</summary>
    Below,

    /// <summary>Badges on the artwork, always visible.</summary>
    Overlay,

    /// <summary>Badges on the artwork, shown on hover or focus. Always visible on touch screens, which have no hover.</summary>
    Hover
}

/// <summary>How a card reacts to the pointer. Keyboard and remote focus always keep their outline.</summary>
public enum CardHoverStyle
{
    /// <summary>Accent outline, the default.</summary>
    Outline,

    /// <summary>The artwork grows slightly.</summary>
    Zoom,

    /// <summary>The artwork rises with a deeper shadow.</summary>
    Lift
}

/// <summary>Kinds of media that can have a card style of their own.</summary>
public enum CardMediaGroup
{
    Movies,
    Series,
    Music
}
