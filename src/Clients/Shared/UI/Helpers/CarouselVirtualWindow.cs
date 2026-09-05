namespace K7.Clients.Shared.UI.Helpers;

public static class CarouselVirtualWindow
{
    public const int DefaultOverscan = 4;
    public const int DefaultInitialVisibleCount = 8;

    public static (int First, int Last) FromVisibleRange(
        int firstVisible,
        int lastVisible,
        int overscan,
        int itemCount)
    {
        if (itemCount <= 0)
            return (0, -1);

        var start = Math.Min(firstVisible, lastVisible);
        var end = Math.Max(firstVisible, lastVisible);
        var first = Math.Max(0, start - overscan);
        var last = Math.Min(itemCount - 1, end + overscan);
        if (first > last)
            return (0, Math.Min(itemCount - 1, DefaultInitialVisibleCount - 1));

        return (first, last);
    }

    public static (int First, int Last) FromAnchor(
        int anchorIndex,
        int overscan,
        int itemCount,
        int visibleCount = DefaultInitialVisibleCount)
    {
        if (itemCount <= 0)
            return (0, -1);

        var anchor = Math.Clamp(anchorIndex, 0, itemCount - 1);
        var lastVisible = Math.Min(itemCount - 1, anchor + Math.Max(visibleCount - 1, 0));
        return FromVisibleRange(anchor, lastVisible, overscan, itemCount);
    }

    public static bool Contains(int first, int last, int index) =>
        index >= first && index <= last;
}
