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
    [CascadingParameter] private ExploreFocusNavigationContext? ExploreFocus { get; set; }
    [CascadingParameter] private TvFeedRowViewport? RowViewport { get; set; }

    private List<LiteMediaDto> _items = [];
    private List<CarouselCardItem> _cardItems = [];
    private bool _loading = true;
    private bool _renderCarousel => RowViewport?.RenderContent ?? true;
    private MetadataPictureSize CardPictureSize =>
        MetadataPictureDisplayHelper.SizeForBrowsePoster(TvFocus is not null);
    private string? _loadKey;
    private bool _canExclude;
    private bool _canSetWatchState;
    private bool _isAdmin;
    private IDisposable? _hubSubscription;
    private DebouncedActionRunner? _visualRefreshRunner;
    private readonly HashSet<Guid> _pendingVisualMediaIds = [];
    private readonly SuppressRenderEventHandler _silentFocus = new();

    protected override async Task OnInitializedAsync()
    {
        (_canExclude, _isAdmin) = await MediaCardExcludeActions.LoadPermissionsAsync(FeatureAccess);
        _canSetWatchState = await WatchStateActions.CanSetWatchStateAsync(FeatureAccess);

        _visualRefreshRunner = new DebouncedActionRunner(RefreshPendingMediaVisualsAsync, InvokeAsync);
        _hubSubscription = HubCoordinator.Subscribe(
            LibraryIds,
            LibraryGroupIds,
            onCatalogChanged: () => ReloadAsync().FireAndForget(Logger),
            onMediaVisualChanged: OnMediaVisualChanged,
            mediaTypes: MediaTypes);
    }

    public void Dispose()
    {
        _hubSubscription?.Dispose();
        _visualRefreshRunner?.Dispose();
    }

    private void OnMediaVisualChanged(Guid mediaId)
    {
        if (!_items.Any(i => i.Id == mediaId))
            return;

        _pendingVisualMediaIds.Add(mediaId);
        _visualRefreshRunner?.Schedule();
    }

    private async Task RefreshPendingMediaVisualsAsync()
    {
        if (_pendingVisualMediaIds.Count == 0)
            return;

        var mediaIds = _pendingVisualMediaIds.ToArray();
        _pendingVisualMediaIds.Clear();

        try
        {
            var page = await MediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
            {
                Ids = mediaIds,
                LibraryIds = LibraryGroupIds is { Length: > 0 } ? null : LibraryIds,
                LibraryGroupIds = LibraryGroupIds,
                PageNumber = 1,
                PageSize = mediaIds.Length
            });

            if (page?.Items is not { Count: > 0 })
                return;

            var changed = false;
            foreach (var item in page.Items)
            {
                if (UpsertCard(item))
                    changed = true;
            }

            if (changed)
                StateHasChanged();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            // Best-effort soft refresh; next catalog reload will pick up fresh data.
        }
    }

    private bool UpsertCard(LiteMediaDto item)
    {
        var model = item.ToCardViewModel(ApiClient, n => string.Format(S["SeasonNumber"], n), pictureSize: CardPictureSize);
        if (model is null)
            return false;

        for (var i = 0; i < _items.Count; i++)
        {
            if (_items[i].Id != item.Id)
                continue;

            _items[i] = item;
            var existing = _cardItems[i].Model;
            var merge = MediaCardVisualMerge.Apply(existing, model);
            _cardItems[i] = new CarouselCardItem(item.Id, merge.Model, GetVariant(item), GetHref(item));
            return merge.RequiresRender;
        }

        return false;
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
                ApplyItems(page.Items.DistinctBy(item => item.Id).ToList());
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

    private void ApplyItems(List<LiteMediaDto> nextItems)
    {
        var existingById = _cardItems.DistinctBy(x => x.Id).ToDictionary(x => x.Id, x => x.Model);
        _items = nextItems;
        _cardItems = nextItems
            .Select(item =>
            {
                var model = item.ToCardViewModel(ApiClient, n => string.Format(S["SeasonNumber"], n), pictureSize: CardPictureSize);
                if (model is null)
                    return null;

                if (existingById.TryGetValue(item.Id, out var existing))
                    model = MediaCardVisualMerge.Apply(existing, model).Model;

                return new CarouselCardItem(item.Id, model, GetVariant(item), GetHref(item));
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
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
        LiteMusicTrackDto track => $"/music/albums/{track.AlbumId}#track-{track.Id}",
        LiteSerieDto serie => $"/series/{serie.Id}",
        LiteSerieSeasonDto season => $"/series/{season.SerieId}/seasons/{season.SeasonNumber}",
        LiteSerieEpisodeDto ep => $"/series/{ep.SerieId}/seasons/{ep.SeasonNumber}#ep-{ep.EpisodeNumber}",
        _ => $"/movies/{item.Id}"
    };

    private string? GetCardElementId(Guid mediaId) =>
        ExploreFocus?.GetCardElementId(mediaId.ToString());

    private EventCallback CreateSilentCardFocusCallback(MediaCardViewModel item) =>
        EventCallback.Factory.Create(_silentFocus, () => OnCardFocused(item));

    private void OnCardFocused(MediaCardViewModel item)
    {
        ExploreFocus?.SaveMediaId(item.Id);
        TvFocus?.OnItemFocused(item);
    }

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
