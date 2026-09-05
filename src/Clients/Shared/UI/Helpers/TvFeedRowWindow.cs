namespace K7.Clients.Shared.UI.Helpers;

public static class TvFeedRowWindow
{
    public const int BackwardOverscan = 1;
    public const int ForwardOverscan = 2;

    public static (int From, int To) InitialRange(
        int activeIndex,
        int backwardOverscan = BackwardOverscan,
        int forwardOverscan = ForwardOverscan)
    {
        var active = Math.Max(0, activeIndex);
        return (Math.Max(0, active - backwardOverscan), active + forwardOverscan);
    }

    public static (int From, int To) Grow(
        int from,
        int to,
        int activeIndex,
        int backwardOverscan = BackwardOverscan,
        int forwardOverscan = ForwardOverscan)
    {
        var (nextFrom, nextTo) = InitialRange(activeIndex, backwardOverscan, forwardOverscan);
        return (Math.Min(from, nextFrom), Math.Max(to, nextTo));
    }

    public static bool ShouldRenderContent(int rowIndex, int from, int to) =>
        rowIndex >= 0 && rowIndex >= from && rowIndex <= to;
}
