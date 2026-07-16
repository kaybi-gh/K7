using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components.Explore;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace K7.Clients.Shared.UI.Components;

public partial class MediasCarouselRow : IDisposable
{
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private IMediaBrowseHubCoordinator HubCoordinator { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private ILogger<MediasCarouselRow> Logger { get; set; } = default!;

    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;
    [Parameter] public Guid[]? LibraryIds { get; set; }
    [Parameter] public Guid[]? LibraryGroupIds { get; set; }
    [Parameter] public HashSet<MediaType>? MediaTypes { get; set; }
    [Parameter] public string[]? Genres { get; set; }
    [Parameter, EditorRequired] public HashSet<MediaOrderingOption> OrderBy { get; set; } = [];
    [Parameter] public bool UnwatchedOnly { get; set; }
    [Parameter] public int PageSize { get; set; } = 20;
    [Parameter] public bool ProgressEnabled { get; set; }

    [CascadingParameter] private ExploreTvFocusContext? TvFocus { get; set; }

    private List<LiteMediaDto> _items = [];
    private List<CarouselCardItem> _cardItems = [];
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

        _hubSubscription = HubCoordinator.Subscribe(LibraryIds, LibraryGroupIds, () => ReloadAsync().FireAndForget(Logger));
    }

    public void Dispose() => _hubSubscription?.Dispose();

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
            _cardItems = [];
        }

        var query = new GetMediasWithPaginationQuery
        {
            LibraryIds = LibraryGroupIds is { Length: > 0 } ? null : LibraryIds,
            LibraryGroupIds = LibraryGroupIds,
            MediaTypes = MediaTypes,
            Genres = Genres,
            OrderBy = OrderBy,
            UnwatchedOnly = UnwatchedOnly ? true : null,
            PageNumber = 1,
            PageSize = PageSize
        };

        try
        {
            var page = await MediaService.GetLiteMediasAsync(query);
            if (page?.Items is not null)
            {
                _items = page.Items.ToList();
                _cardItems = _items
                    .Select(item => (Item: item, Model: item.ToCardViewModel(ApiClient, n => string.Format(S["SeasonNumber"], n))))
                    .Where(item => item.Model is not null)
                    .Select(item => new CarouselCardItem(item.Item.Id, item.Model!, GetVariant(item.Item), GetHref(item.Item)))
                    .ToList();
                if (_cardItems.Count > 0)
                    TvFocus?.TrySetInitialItem(_cardItems[0].Model);
            }
        }
        catch
        {
            _items = [];
            _cardItems = [];
        }

        _loading = false;
    }

    private async Task ReloadAsync()
    {
        _loadKey = null;
        await OnParametersSetAsync();
        await InvokeAsync(StateHasChanged);
    }

    private string BuildLoadKey() => string.Join('|',
        Title,
        string.Join(',', LibraryGroupIds ?? []),
        string.Join(',', LibraryIds ?? []),
        string.Join(',', MediaTypes ?? []),
        string.Join(',', Genres ?? []),
        string.Join(',', OrderBy),
        UnwatchedOnly,
        PageSize,
        ProgressEnabled);

    private static MediaCardVariant GetVariant(LiteMediaDto item) => item switch
    {
        LiteMusicAlbumDto or LiteMusicTrackDto or LiteMusicArtistDto => MediaCardVariant.Cover,
        _ => MediaCardVariant.Poster
    };

    private static string GetHref(LiteMediaDto item) => item switch
    {
        LiteMusicArtistDto artist => $"/music/artists/{artist.Id}",
        LiteMusicAlbumDto album => $"/music/albums/{album.Id}",
        LiteMusicTrackDto track => $"/music/albums/{track.AlbumId}",
        LiteSerieDto serie => $"/series/{serie.Id}",
        LiteSerieSeasonDto season => $"/series/{season.SerieId}/seasons/{season.SeasonNumber}",
        LiteSerieEpisodeDto ep => $"/series/{ep.SerieId}/seasons/{ep.SeasonNumber}#ep-{ep.EpisodeNumber}",
        _ => $"/movies/{item.Id}"
    };

    private async Task ExcludeForSelf(MediaCardViewModel item)
    {
        if (await MediaCardExcludeActions.ExcludeForSelfAsync(item, UserAdminService, Snackbar, S))
        {
            _items.RemoveAll(x => x.Id.ToString() == item.Id || x.Id.ToString() == item.ParentId);
            _cardItems.RemoveAll(x => x.Model.Id == item.Id || x.Model.ParentId == item.Id);
        }
    }

    private Task ExcludeForOthers(MediaCardViewModel item) =>
        MediaCardExcludeActions.ExcludeForOthersAsync(item, DialogService, Snackbar, S);

    private sealed record CarouselCardItem(
        Guid Id,
        MediaCardViewModel Model,
        MediaCardVariant Variant,
        string Href);
}
