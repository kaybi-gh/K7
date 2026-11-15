using K7.Shared.Enums;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// The mini music player is mounted twice, in the navigation bar and in the bottom dock, and
/// only the one whose media query matches shows. These are the queries for a placement, so both
/// slots always agree: exactly one of them matches at any width.
/// </summary>
public static class MiniPlayerSlots
{
    private const string Wide = "(min-width: 600px)";
    private const string Narrow = "(max-width: 599.98px)";
    private const string Always = "all";
    private const string Never = "not all";

    /// <summary>Slot inside the navigation bar: wide screens, unless the player was moved to the bottom.</summary>
    public static string BarQuery(MiniPlayerPlacement placement) =>
        placement == MiniPlayerPlacement.Bottom ? Never : Wide;

    /// <summary>Slot at the bottom: phones always, and every screen when the player was moved there.</summary>
    public static string DockQuery(MiniPlayerPlacement placement) =>
        placement == MiniPlayerPlacement.Bottom ? Always : Narrow;
}
