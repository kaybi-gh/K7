using K7.Clients.Shared.Models;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using K7.Shared.Home;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// Ready-made home rows offered when adding a row: the "smart" rows of the default home, which
/// cannot be rebuilt by hand in the row dialog (their title is a key the app translates, and
/// some are simpler to add than to rebuild). A preset already on the home cannot be added twice.
/// </summary>
public static class HomeRowPresets
{
    private const int DefaultPageSize = 20;

    // Same ids as the default home of the server, so a re-added row is the row it replaces.
    private static readonly Guid _continueWatchingId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid _recommendedId = new("00000000-0000-0000-0000-000000000002");

    /// <summary>A ready-made row, and whether the home already has it (it is then shown but cannot be added twice).</summary>
    public sealed record Preset(HomeRowEditModel Row, bool OnHome);

    /// <summary>The ready-made rows that are not on the home yet.</summary>
    public static List<HomeRowEditModel> Available(
        IReadOnlyCollection<HomeRowEditModel> rows,
        IEnumerable<LibraryGroupDto> libraryGroups) =>
        All(rows, libraryGroups).Where(preset => !preset.OnHome).Select(preset => preset.Row).ToList();

    /// <summary>
    /// Every ready-made row, in menu order. The ones already on the home are kept in the list, so
    /// the menu always shows what exists instead of hiding it once everything is added.
    /// </summary>
    public static List<Preset> All(
        IReadOnlyCollection<HomeRowEditModel> rows,
        IEnumerable<LibraryGroupDto> libraryGroups)
    {
        var presets = new List<HomeRowEditModel>
        {
            new()
            {
                Id = _continueWatchingId,
                Title = HomeLayoutRowTitles.ContinueWatching,
                ContinueWatching = true,
                OrderBy = MediaOrderingOption.LastInteractedDesc,
                PageSize = DefaultPageSize
            },
            new()
            {
                Id = _recommendedId,
                Title = HomeLayoutRowTitles.RecommendedForYou,
                OrderBy = MediaOrderingOption.RecommendedForYou,
                PageSize = DefaultPageSize
            }
        };

        presets.AddRange(libraryGroups.OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase).Select(group => new HomeRowEditModel
        {
            Id = group.Id,
            Title = HomeLayoutRowTitles.NewlyAddedIn(group.Title),
            LibraryGroupIds = [group.Id],
            OrderBy = MediaOrderingOption.CreatedDesc,
            PageSize = DefaultPageSize
        }));

        var titles = rows.Select(r => r.Title).ToHashSet(StringComparer.Ordinal);
        var ids = rows.Select(r => r.Id).ToHashSet();
        return presets
            .Select(preset => titles.Contains(preset.Title)
                ? new Preset(preset, OnHome: true)
                : new Preset(ids.Contains(preset.Id) ? Copy(preset, Guid.NewGuid()) : preset, OnHome: false))
            .ToList();
    }

    private static HomeRowEditModel Copy(HomeRowEditModel preset, Guid id) => new()
    {
        Id = id,
        Title = preset.Title,
        ContinueWatching = preset.ContinueWatching,
        LibraryGroupIds = [.. preset.LibraryGroupIds],
        OrderBy = preset.OrderBy,
        PageSize = preset.PageSize
    };
}
