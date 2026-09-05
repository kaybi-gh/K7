using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Explore;

public partial class ExploreTvHeroPanel : IAsyncDisposable
{
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter] public string? GroupTitle { get; set; }
    [Parameter] public string? BrowseHref { get; set; }
    [Parameter] public string? BrowseLabel { get; set; }
    [Parameter] public string? BackAriaLabel { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }

    private MediaCardViewModel? _focusedItem;
    private int _focusGeneration;
    private bool _hasStartedHeroFetch;
    private readonly Dictionary<string, MediaCardViewModel> _heroDetailCache = new(StringComparer.Ordinal);
    private CancellationTokenSource? _disposeCts;
    private CancellationTokenSource? _fetchCts;
    private DebouncedActionRunner? _heroFetchRunner;

    private bool ShowGroupChrome =>
        !string.IsNullOrEmpty(GroupTitle)
        && !string.IsNullOrEmpty(BrowseHref)
        && !string.IsNullOrEmpty(BrowseLabel);

    protected override void OnInitialized()
    {
        _heroFetchRunner = new DebouncedActionRunner(
            EnrichFocusedItemAsync,
            InvokeAsync,
            TvHeroFocusSettle.DelayMs);
    }

    public void NotifyFocused(MediaCardViewModel item) => _ = FocusItemAsync(item);

    public void TrySetInitialItem(MediaCardViewModel item)
    {
        if (_focusedItem is not null)
            return;

        _ = FocusItemAsync(item);
    }

    private Task HandleBack() => OnBack.InvokeAsync();

    private void GoToBrowse()
    {
        if (!string.IsNullOrEmpty(BrowseHref))
            Navigation.NavigateTo(BrowseHref);
    }

    private async Task FocusItemAsync(MediaCardViewModel item)
    {
        if (_focusedItem?.Id == item.Id
            && _focusedItem.HasHeroDetails()
            && string.Equals(item.ResolveHeroBackdropUrl(), _focusedItem.ResolveHeroBackdropUrl(), StringComparison.Ordinal))
            return;

        var generation = ++_focusGeneration;
        _focusedItem = item;
        CancelInFlightHeroFetch();
        await InvokeAsync(StateHasChanged);

        if (item.HasHeroDetails())
            return;

        if (_heroDetailCache.TryGetValue(item.Id, out var cached))
        {
            if (generation != _focusGeneration)
                return;

            _focusedItem = cached;
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (!Guid.TryParse(item.Id, out _))
            return;

        if (!_hasStartedHeroFetch)
        {
            _hasStartedHeroFetch = true;
            await EnrichFocusedItemAsync();
            return;
        }

        _heroFetchRunner?.Schedule();
    }

    private async Task EnrichFocusedItemAsync()
    {
        var item = _focusedItem;
        if (item is null || item.HasHeroDetails())
            return;

        if (_heroDetailCache.TryGetValue(item.Id, out var cached))
        {
            _focusedItem = cached;
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (!Guid.TryParse(item.Id, out var mediaId))
            return;

        var generation = _focusGeneration;
        _fetchCts?.Dispose();
        _fetchCts = CancellationTokenSource.CreateLinkedTokenSource(GetCancellationToken());
        var ct = _fetchCts.Token;

        try
        {
            var media = await MediaService.GetMediaAsync(mediaId, ct);
            if (media is null || generation != _focusGeneration)
                return;

            var enriched = item.WithHeroDetailsFromMedia(media, ApiClient);
            _heroDetailCache[item.Id] = enriched;
            _focusedItem = enriched;
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
    }

    private void CancelInFlightHeroFetch()
    {
        _fetchCts?.Cancel();
        _fetchCts?.Dispose();
        _fetchCts = null;
    }

    private CancellationToken GetCancellationToken()
    {
        _disposeCts ??= new CancellationTokenSource();
        return _disposeCts.Token;
    }

    public async ValueTask DisposeAsync()
    {
        _heroFetchRunner?.Dispose();
        _heroFetchRunner = null;
        CancelInFlightHeroFetch();
        if (_disposeCts is not null)
        {
            await _disposeCts.CancelAsync();
            _disposeCts.Dispose();
        }
    }
}
