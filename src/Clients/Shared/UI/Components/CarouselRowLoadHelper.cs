namespace K7.Clients.Shared.UI.Components;

internal static class CarouselRowLoadHelper
{
    public static bool ShouldReload(string? cachedKey, string newKey, int itemCount) =>
        cachedKey != newKey || itemCount == 0;
}
