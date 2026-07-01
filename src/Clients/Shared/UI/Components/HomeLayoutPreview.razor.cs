using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Helpers;
using K7.Clients.Shared.UI.Pages;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components;

public partial class HomeLayoutPreview
{
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private IStringLocalizer<Home> HomeL { get; set; } = default!;

    [Parameter] public IReadOnlyList<HomeRowEditModel> Rows { get; set; } = [];

    private List<HomeRowConfigDto> _rows = [];
    private List<(string Title, List<MediaCardViewModel> Items)> _loaded = [];
    private bool _loading;

    public async Task RefreshAsync()
    {
        _rows = Rows
            .Where(r => r.IsVisible)
            .OrderBy(r => r.Order)
            .Select(r => r.ToDto())
            .ToList();

        _loaded = _rows.Select(r => (HomeLayoutRowTitleHelper.Localize(HomeL, r.Title), new List<MediaCardViewModel>())).ToList();
        _loading = true;

        var tasks = _loaded
            .Select((entry, i) => LoadRowAsync(_rows[i], entry.Items))
            .ToList();

        await Task.WhenAll(tasks);
        _loading = false;
    }

    private async Task LoadRowAsync(HomeRowConfigDto config, List<MediaCardViewModel> target)
    {
        var query = new GetHomeFeedQuery
        {
            ContinueWatching = config.ContinueWatching ? true : null,
            LibraryIds = config.LibraryIds?.ToArray(),
            MediaTypes = config.MediaTypes is { Count: > 0 } mt ? mt.ToHashSet() : null,
            OrderBy = config.OrderBy is { Count: > 0 } ob ? ob.ToHashSet() : null,
            PageNumber = 1,
            PageSize = config.PageSize
        };

        try
        {
            var feedPage = await MediaService.GetHomeFeedAsync(query);
            if (feedPage?.Items is null) return;

            foreach (var item in feedPage.Items)
            {
                target.Add(item.ToCardViewModel(ApiClient));
            }
        }
        catch { }
    }

    private static MediaCardVariant GetVariant(MediaCardViewModel item) => item.Kind == MediaCardKind.Cover
        ? MediaCardVariant.Cover
        : MediaCardVariant.Poster;
}
