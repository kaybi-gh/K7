using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Pages;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using K7.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// The home page as the rows being edited would show it, kept up to date while they change:
/// order, visibility and display follow at once, and a row is only fetched again when what it
/// lists changed (its scope, sort, number of items). It is a reduced picture to look at, not to
/// use: nothing in it takes the focus or a click, and banners stay still.
/// </summary>
public partial class HomeLayoutPreview
{
    // What each row lists, by feed query: reordering, hiding or restyling a row fetches nothing.
    private readonly Dictionary<string, List<MediaCardViewModel>> _itemsByQuery = [];
    private readonly HashSet<string> _loading = [];
    private List<HomeRowConfigDto> _visible = [];

    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private IStringLocalizer<Home> HomeL { get; set; } = default!;

    /// <summary>The rows being edited. Read again on every render of the host.</summary>
    [Parameter] public IReadOnlyList<HomeRowEditModel> Rows { get; set; } = [];

    protected override void OnParametersSet()
    {
        _visible = Rows
            .Where(r => r.IsVisible)
            .OrderBy(r => r.Order)
            .Select(r => r.ToDto())
            .ToList();

        foreach (var row in _visible)
        {
            var key = QueryKey(row);
            if (_itemsByQuery.ContainsKey(key) || !_loading.Add(key))
                continue;

            _ = LoadAsync(key, BuildQuery(row));
        }
    }

    private List<MediaCardViewModel>? ItemsOf(HomeRowConfigDto row) =>
        _itemsByQuery.TryGetValue(QueryKey(row), out var items) ? items : null;

    private async Task LoadAsync(string key, GetHomeFeedQuery query)
    {
        var items = new List<MediaCardViewModel>();
        try
        {
            var page = await MediaService.GetHomeFeedAsync(query);
            if (page?.Items is not null)
                items.AddRange(page.Items.Select(item => item.ToCardViewModel(ApiClient)));
        }
        catch (Exception)
        {
            // A row that cannot load shows as empty in the preview: the editor keeps working.
        }

        _itemsByQuery[key] = items;
        _loading.Remove(key);
        await InvokeAsync(StateHasChanged);
    }

    private static GetHomeFeedQuery BuildQuery(HomeRowConfigDto config)
    {
        var libraryGroupIds = config.LibraryGroupIds is { Count: > 0 } groups ? groups.ToArray() : null;
        return new GetHomeFeedQuery
        {
            ContinueWatching = config.ContinueWatching ? true : null,
            LibraryIds = libraryGroupIds is not null ? null : config.LibraryIds?.ToArray(),
            LibraryGroupIds = libraryGroupIds,
            MediaTypes = config.MediaTypes is { Count: > 0 } mt ? mt.ToHashSet() : null,
            OrderBy = config.OrderBy is { Count: > 0 } ob ? ob.ToHashSet() : null,
            Detailed = config.DisplayType.IsBanner(),
            PageNumber = 1,
            PageSize = config.PageSize
        };
    }

    private static string QueryKey(HomeRowConfigDto config) => string.Join('|',
        config.ContinueWatching,
        string.Join(',', config.LibraryGroupIds ?? []),
        string.Join(',', config.LibraryIds ?? []),
        string.Join(',', config.MediaTypes ?? []),
        string.Join(',', config.OrderBy ?? []),
        config.DisplayType.IsBanner(),
        config.PageSize);
}
