namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// Column math for <see cref="Components.K7VirtualGrid{TItem}"/>.
/// Compact min widths mirror mobile tokens in tokens.css.
/// </summary>
public static class VirtualGridLayout
{
    public const int CompactBreakpoint = 600;
    public const int CompactSpacing = 12;
    public const int DesktopMinSpacing = 24;

    /// <summary>Matches --media-card-grid-column-min-poster (108px).</summary>
    public const int CompactPosterColumnMin = 108;

    /// <summary>Matches --media-card-grid-column-min-backdrop (140px).</summary>
    public const int CompactBackdropColumnMin = 140;

    public const int BackdropMinColumns = 2;
    public const int CompactBackdropMaxColumns = 2;

    public static int GetEffectiveSpacing(int containerWidth, int spacing) =>
        containerWidth > 0 && containerWidth < CompactBreakpoint
            ? CompactSpacing
            : Math.Max(spacing, DesktopMinSpacing);

    public static int GetColumnWidthFloor(int containerWidth, int itemWidth, float aspectRatio)
    {
        if (containerWidth <= 0 || containerWidth >= CompactBreakpoint)
        {
            return itemWidth;
        }

        var compactMin = aspectRatio < 1f ? CompactBackdropColumnMin : CompactPosterColumnMin;
        return Math.Min(itemWidth, compactMin);
    }

    public static int CalculateColumnCount(int containerWidth, int itemWidth, int spacing, float aspectRatio, int? maxColumnCount = null)
    {
        if (containerWidth <= 0)
        {
            return 4;
        }

        var effectiveSpacing = GetEffectiveSpacing(containerWidth, spacing);
        var floor = GetColumnWidthFloor(containerWidth, itemWidth, aspectRatio);
        var cols = Math.Max((containerWidth + effectiveSpacing) / (floor + effectiveSpacing), 1);

        if (aspectRatio < 1f)
        {
            cols = Math.Max(cols, BackdropMinColumns);
            if (containerWidth < CompactBreakpoint)
                cols = Math.Min(cols, CompactBackdropMaxColumns);
        }

        if (maxColumnCount is > 0)
            cols = Math.Min(cols, maxColumnCount.Value);

        return cols;
    }
}
