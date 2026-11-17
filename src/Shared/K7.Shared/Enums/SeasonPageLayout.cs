namespace K7.Shared.Enums;

/// <summary>
/// How the page of a season shows its episodes. Stored in preferences: only add values at the end.
/// </summary>
public enum SeasonPageLayout
{
    /// <summary>The default: the list, like <see cref="List"/>.</summary>
    Auto,

    /// <summary>The episodes in a list under the title of the season.</summary>
    List,

    /// <summary>
    /// The episode in focus on the whole screen, the episodes in a carousel: the page of TVs,
    /// made for a remote. TVs always show it, whatever the setting, and no other device does:
    /// it is not a choice of the settings, and a device set to it shows the list.
    /// </summary>
    Carousel,

    /// <summary>The episodes as a grid of cards under the title of the season.</summary>
    Grid
}
