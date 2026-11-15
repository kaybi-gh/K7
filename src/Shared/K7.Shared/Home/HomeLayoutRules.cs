using K7.Shared.Dtos.Home;
using K7.Shared.Enums;

namespace K7.Shared.Home;

/// <summary>
/// Rules a home layout follows whatever edits it: a home has no banner or one banner, and the
/// banner is always the first row. One rule for the editor, the validators and stored layouts.
/// </summary>
public static class HomeLayoutRules
{
    /// <summary>True when there is at most one banner and nothing is placed before it.</summary>
    public static bool HasValidBanner(IReadOnlyCollection<HomeRowConfigDto> rows)
    {
        var banners = rows.Where(r => r.DisplayType.IsBanner()).ToList();
        return banners.Count switch
        {
            0 => true,
            1 => banners[0].Order <= rows.Min(r => r.Order),
            _ => false
        };
    }

    /// <summary>
    /// Puts the banner first and turns any other banner back into a poster carousel, keeping the
    /// rest in order. Rows are renumbered from zero.
    /// </summary>
    public static List<HomeRowConfigDto> ApplyBannerRule(IEnumerable<HomeRowConfigDto> rows)
    {
        var ordered = rows.OrderBy(r => r.Order).ToList();
        var banner = ordered.FirstOrDefault(r => r.DisplayType.IsBanner());
        if (banner is null)
            return Renumber(ordered);

        var others = ordered
            .Where(r => !ReferenceEquals(r, banner))
            .Select(r => r.DisplayType.IsBanner()
                ? r with { DisplayType = HomeRowDisplayType.Carousel, AutoAdvanceSeconds = null }
                : r);
        return Renumber(others.Prepend(banner));
    }

    private static List<HomeRowConfigDto> Renumber(IEnumerable<HomeRowConfigDto> rows) =>
        rows.Select((row, index) => row with { Order = index }).ToList();
}
