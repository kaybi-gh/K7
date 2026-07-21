using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Explore;

public partial class ExploreTvHeroPanel : IAsyncDisposable
{
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;

    [Parameter] public string? GroupTitle { get; set; }
    [Parameter] public string? BrowseHref { get; set; }
    [Parameter] public string? BrowseLabel { get; set; }
    [Parameter] public string? BackAriaLabel { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }

    private MediaCardViewModel? _focusedItem;
    private int _focusGeneration;
    private readonly Dictionary<string, MediaCardViewModel> _heroDetailCache = new(StringComparer.Ordinal);
    private CancellationTokenSource? _disposeCts;

    private bool ShowGroupChrome =>
        !string.IsNullOrEmpty(GroupTitle)
        && !string.IsNullOrEmpty(BrowseHref)
        && !string.IsNullOrEmpty(BrowseLabel);

    public void NotifyFocused(MediaCardViewModel item) => _ = FocusItemAsync(item);

    public void TrySetInitialItem(MediaCardViewModel item)
    {
        if (_focusedItem is not null)
            return;

        _ = FocusItemAsync(item);
    }

    private Task HandleBack() => OnBack.InvokeAsync();

    private async Task FocusItemAsync(MediaCardViewModel item)
    {
        if (_focusedItem?.Id == item.Id
            && _focusedItem.HasHeroDetails()
            && string.Equals(item.ResolveHeroBackdropUrl(), _focusedItem.ResolveHeroBackdropUrl(), StringComparison.Ordinal))
            return;

        var generation = ++_focusGeneration;
        _focusedItem = item;
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

        if (!Guid.TryParse(item.Id, out var mediaId))
            return;

        try
        {
            var media = await MediaService.GetMediaAsync(mediaId, GetCancellationToken());
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

    private CancellationToken GetCancellationToken()
    {
        _disposeCts ??= new CancellationTokenSource();
        return _disposeCts.Token;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposeCts is not null)
        {
            await _disposeCts.CancelAsync();
            _disposeCts.Dispose();
        }
    }
}
