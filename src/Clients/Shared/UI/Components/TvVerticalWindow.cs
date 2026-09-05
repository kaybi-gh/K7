using K7.Clients.Shared.UI.Helpers;

namespace K7.Clients.Shared.UI.Components;

public sealed class TvVerticalWindow
{
    public int ActiveIndex { get; private set; }
    public int MountedFrom { get; private set; }
    public int MountedTo { get; private set; }

    public void Reset(int activeIndex)
    {
        ActiveIndex = Math.Max(0, activeIndex);
        (MountedFrom, MountedTo) = TvFeedRowWindow.InitialRange(ActiveIndex);
    }

    /// <summary>
    /// Expands the mounted range around <paramref name="activeIndex"/>. Never shrinks.
    /// Returns true only when new rows must mount.
    /// </summary>
    public bool GrowTo(int activeIndex)
    {
        ActiveIndex = Math.Max(0, activeIndex);
        var (from, to) = TvFeedRowWindow.Grow(MountedFrom, MountedTo, ActiveIndex);
        if (from == MountedFrom && to == MountedTo)
            return false;

        MountedFrom = from;
        MountedTo = to;
        return true;
    }

    public bool ShouldRender(int rowIndex) =>
        TvFeedRowWindow.ShouldRenderContent(rowIndex, MountedFrom, MountedTo);
}
