using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components.Explore;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class HomeFeedCarouselRow : IDisposable
{
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private IMediaBrowseHubCoordinator HubCoordinator { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;
    [Parameter] public Guid[]? LibraryIds { get; set; }
    [Parameter] public Guid[]? LibraryGroupIds { get; set; }
    [Parameter] public HashSet<MediaType>? MediaTypes { get; set; }
    [Parameter, EditorRequired] public HashSet<MediaOrderingOption> OrderBy { get; set; } = [];
    [Parameter] public int PageSize { get; set; } = 20;
    [Parameter] public bool ProgressEnabled { get; set; }

    [CascadingParameter] private ExploreTvFocusContext? TvFocus { get; set; }

    private List<MediaCardViewModel> _items = [];
    private bool _loading = true;
    private string? _loadKey;
    private bool _canExclude;
    private bool _canSetWatchState;
    private bool _isAdmin;
    private IDisposable? _hubSubscription;

    protected override async Task OnInitializedAsync()
    {
        (_canExclude, _isAdmin) = await MediaCardExcludeActions.LoadPermissionsAsync(FeatureAccess);
        _canSetWatchState = await WatchStateActions.CanSetWatchStateAsync(FeatureAccess);

        _hubSubscription = HubCoordinator.Subscribe(LibraryIds, LibraryGroupIds, () => _ = ReloadAsync());
    }

    public void Dispose() => _hubSubscription?.Dispose();

    private async Task ReloadAsync()
    {
        _loadKey = null;
        await OnParametersSetAsync();
        await InvokeAsync(StateHasChanged);
    }

    protected override async Task OnParametersSetAsync()
    {
        var key = BuildLoadKey();
        if (!CarouselRowLoadHelper.ShouldReload(_loadKey, key, _items.Count))
            return;

        _loadKey = key;
        var isFirstLoad = _items.Count == 0;
        if (isFirstLoad)
        {
            _loading = true;
            _items = [];
        }

        var query = new GetHomeFeedQuery
        {
            LibraryIds = LibraryGroupIds is { Length: > 0 } ? null : LibraryIds,
            LibraryGroupIds = LibraryGroupIds,
            MediaTypes = MediaTypes,
            OrderBy = OrderBy,
            Detailed = TvFocus?.UseDetailedFeed ?? false,
            PageNumber = 1,
            PageSize = PageSize
        };

        try
        {
            var feedPage = await MediaService.GetHomeFeedAsync(query);
            if (feedPage?.Items is not null)
            {
                _items = feedPage.Items.Select(item => item.ToCardViewModel(ApiClient)).ToList();
                if (_items.Count > 0)
                    TvFocus?.TrySetInitialItem(_items[0]);
            }
        }
        catch
        {
            _items = [];
        }

        _loading = false;
    }

    private string BuildLoadKey() => string.Join('|',
        Title,
        string.Join(',', LibraryGroupIds ?? []),
        string.Join(',', LibraryIds ?? []),
        string.Join(',', MediaTypes ?? []),
        string.Join(',', OrderBy),
        PageSize,
        ProgressEnabled,
        TvFocus?.UseDetailedFeed ?? false);

    private static MediaCardVariant GetVariant(MediaCardViewModel item) => item.Kind switch
    {
        MediaCardKind.Cover => MediaCardVariant.Cover,
        _ => MediaCardVariant.Poster
    };

    private string GetHref(MediaCardViewModel item) => item.NavigationTarget ?? item.Kind switch
    {
        MediaCardKind.Cover => $"/music/albums/{item.ParentId ?? item.Id}",
        MediaCardKind.Serie => $"/series/{item.Id}",
        MediaCardKind.Season => $"/series/{item.ParentId ?? item.Id}/seasons/{item.SeasonNumber}",
        MediaCardKind.Episode => $"/series/{item.ParentId ?? item.Id}/seasons/{item.SeasonNumber}#ep-{item.EpisodeNumber}",
        _ => $"/movies/{item.Id}"
    };

    private async Task ExcludeForSelf(MediaCardViewModel item)
    {
        if (await MediaCardExcludeActions.ExcludeForSelfAsync(item, UserAdminService, Snackbar, S))
            _items.RemoveAll(x => x.Id == item.Id || x.ParentId == item.Id);
    }

    private Task ExcludeForOthers(MediaCardViewModel item) =>
        MediaCardExcludeActions.ExcludeForOthersAsync(item, DialogService, Snackbar, S);

    private async Task RefreshAfterWatchStateChangeAsync()
    {
        _loadKey = null;
        await OnParametersSetAsync();
    }
}
