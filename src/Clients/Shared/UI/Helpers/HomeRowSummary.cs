using K7.Clients.Shared.Models;
using K7.Shared.Dtos.Entities;
using K7.Shared.Enums;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// The configuration of a home row in two short lines, for the row list: how it shows, then what
/// it lists. Labels come from the row dialog, so the list and the dialog use the same words.
/// </summary>
public static class HomeRowSummary
{
    /// <summary>Separator between two parts (a middle dot).</summary>
    public const string Separator = " · ";

    private const int MaxNames = 2;

    /// <summary>"Carousel - Posters", or "Banner - Full bleed - every 8 s".</summary>
    public static string Display(HomeRowEditModel row, IStringLocalizer dialog, IStringLocalizer list)
    {
        var parts = new List<string>();
        if (row.DisplayType.IsBanner())
        {
            parts.Add(dialog["KindBanner"]);
            parts.Add(dialog[row.DisplayType == HomeRowDisplayType.FeaturedBleed ? "BannerBleed" : "BannerContained"]);
            if (row.AutoAdvanceSeconds is > 0)
                parts.Add(string.Format(dialog["AutoAdvanceSeconds"], row.AutoAdvanceSeconds));
        }
        else
        {
            parts.Add(dialog["KindCarousel"]);
            parts.Add(dialog[row.DisplayType == HomeRowDisplayType.Landscape ? "DisplayLandscape" : "DisplayCarousel"]);
        }

        parts.Add(string.Format(list["SummaryItems"], row.PageSize));
        return string.Join(Separator, parts);
    }

    /// <summary>"Whole catalog - Movies, Series - Recently added", or "Continue watching".</summary>
    public static string Content(
        HomeRowEditModel row,
        IReadOnlyList<LibraryDto> libraries,
        IReadOnlyList<LibraryGroupDto> libraryGroups,
        IStringLocalizer dialog,
        IStringLocalizer list)
    {
        if (row.ContinueWatching)
            return dialog["ContinueWatching"];

        var parts = new List<string> { Scope(row, libraries, libraryGroups, dialog, list) };

        if (row.MediaTypes.Count > 0)
            parts.Add(string.Join(", ", row.MediaTypes.Select(type => Label(dialog, $"MediaType{type}") ?? type.ToString())));

        // Rows of the default home use sorts the dialog does not offer: they have no label here.
        if (Label(dialog, $"Order{row.OrderBy}") is { } sort)
            parts.Add(sort);

        return string.Join(Separator, parts);
    }

    private static string Scope(
        HomeRowEditModel row,
        IReadOnlyList<LibraryDto> libraries,
        IReadOnlyList<LibraryGroupDto> libraryGroups,
        IStringLocalizer dialog,
        IStringLocalizer list)
    {
        if (row.LibraryGroupIds.Count > 0)
        {
            return Names(
                row.LibraryGroupIds.Select(id => libraryGroups.FirstOrDefault(g => g.Id == id)?.Title),
                row.LibraryGroupIds.Count,
                dialog["ScopeLibraryGroups"],
                list);
        }

        if (row.LibraryIds.Count > 0)
        {
            return Names(
                row.LibraryIds.Select(id => libraries.FirstOrDefault(l => l.Id == id)?.Title),
                row.LibraryIds.Count,
                dialog["ScopeLibraries"],
                list);
        }

        return dialog["ScopeAll"];
    }

    // The first names, then "+N". Names not loaded yet (or deleted) fall back to the kind of scope.
    private static string Names(IEnumerable<string?> names, int total, string fallback, IStringLocalizer list)
    {
        var known = names.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).ToList();
        if (known.Count == 0)
            return fallback;

        var shown = string.Join(", ", known.Take(MaxNames));
        var hidden = total - Math.Min(known.Count, MaxNames);
        return hidden > 0 ? shown + " " + string.Format(list["SummaryMore"], hidden) : shown;
    }

    private static string? Label(IStringLocalizer localizer, string key)
    {
        var value = localizer[key];
        return value.ResourceNotFound ? null : value.Value;
    }
}
