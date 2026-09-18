namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// Column math for <see cref="Components.K7VirtualGrid{TItem}"/>.
/// Compact min widths mirror mobile tokens in tokens.css.
/// </summary>
public static class VirtualGridLayout
{
    public const int CompactBreakpoint = 600;
    public const int ShortViewportHeight = 600;
    public const int CompactSpacing = 12;
    public const int DesktopMinSpacing = 24;

    /// <summary>Matches --media-card-grid-column-min-poster (108px).</summary>
    public const int CompactPosterColumnMin = 108;

    /// <summary>Matches --media-card-grid-column-min-backdrop (140px).</summary>
    public const int CompactBackdropColumnMin = 140;

    public const int BackdropMinColumns = 2;
    public const int CompactBackdropMaxColumns = 2;

    private const int FitMinPosterWidth = 72;
    private const int FitRowFooter = 56;
    private const int FitRowGap = 8;

    public static bool IsCompact(int containerWidth, int containerHeight = 0) =>
        (containerWidth > 0 && containerWidth < CompactBreakpoint)
        || (containerHeight > 0 && containerHeight < ShortViewportHeight);

    public static int GetEffectiveSpacing(int containerWidth, int spacing, int containerHeight = 0) =>
        IsCompact(containerWidth, containerHeight)
            ? CompactSpacing
            : Math.Max(spacing, DesktopMinSpacing);

    public static int GetColumnWidthFloor(int containerWidth, int itemWidth, float aspectRatio, int containerHeight = 0)
    {
        if (!IsCompact(containerWidth, containerHeight))
            return itemWidth;

        var compactMin = aspectRatio < 1f ? CompactBackdropColumnMin : CompactPosterColumnMin;
        return Math.Min(itemWidth, compactMin);
    }

    public static int CalculateColumnCount(
        int containerWidth,
        int itemWidth,
        int spacing,
        float aspectRatio,
        int? maxColumnCount = null,
        int containerHeight = 0)
    {
        if (containerWidth <= 0)
        {
            return 4;
        }

        var effectiveSpacing = GetEffectiveSpacing(containerWidth, spacing, containerHeight);
        var floor = GetColumnWidthFloor(containerWidth, itemWidth, aspectRatio, containerHeight);
        var cols = Math.Max((containerWidth + effectiveSpacing) / (floor + effectiveSpacing), 1);

        if (aspectRatio < 1f)
        {
            cols = Math.Max(cols, BackdropMinColumns);
            if (containerWidth > 0 && containerWidth < CompactBreakpoint)
                cols = Math.Min(cols, CompactBackdropMaxColumns);
        }

        cols = FitColumnsToHeight(cols, containerWidth, containerHeight, effectiveSpacing, aspectRatio);

        if (maxColumnCount is > 0)
            cols = Math.Min(cols, maxColumnCount.Value);

        return cols;
    }

    private static int FitColumnsToHeight(
        int cols,
        int containerWidth,
        int containerHeight,
        int spacing,
        float aspectRatio)
    {
        if (containerHeight <= 0 || aspectRatio <= 0)
            return cols;

        var minW = aspectRatio < 1f ? CompactBackdropColumnMin : FitMinPosterWidth;
        while (cols < 16)
        {
            var itemWidth = (containerWidth - (cols - 1) * spacing) / (float)cols;
            if (itemWidth <= minW)
                return cols;

            var rowHeight = itemWidth * aspectRatio + FitRowFooter + FitRowGap;
            if (rowHeight <= containerHeight)
                return cols;

            cols++;
        }

        return cols;
    }
}
