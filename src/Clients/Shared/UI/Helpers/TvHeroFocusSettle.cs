namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// D-pad on TV can cross many cards before the user stops. Hero copy updates
/// immediately. Backdrop JPEG decode waits until focus has settled.
/// </summary>
public static class TvHeroFocusSettle
{
    public const int DelayMs = 200;
}
