using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class HomeTvHero : IAsyncDisposable
{
    private readonly string?[] _layerUrls = [null, null];
    private readonly bool[] _layerSoft = [false, false];
    private int _activeLayer;
    private int _swapGeneration;
    private bool _disposed;
    private MediaCardViewModel? _focused;

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    [Parameter] public MediaCardViewModel? Model { get; set; }

    private MediaCardViewModel? DisplayModel => _focused ?? Model;

    protected override void OnParametersSet()
    {
        if (_focused is null || Model?.Id == _focused.Id)
            _focused = Model;

        _ = SwapBackdropIfNeededAsync();
    }

    /// <summary>
    /// Update the hero without a parent re-render (Home carousels stay mounted).
    /// </summary>
    public void ApplyFocusedItem(MediaCardViewModel item)
    {
        if (_focused?.Id == item.Id)
            return;

        _focused = item;
        StateHasChanged();
        _ = SwapBackdropIfNeededAsync();
    }

    private async Task SwapBackdropIfNeededAsync()
    {
        var newUrl = DisplayModel?.ResolveHeroBackdropUrl();
        var newSoft = ShouldUseSoftBackdrop(DisplayModel);
        if (newUrl == _layerUrls[_activeLayer] && newSoft == _layerSoft[_activeLayer])
            return;

        var targetLayer = 1 - _activeLayer;
        var generation = ++_swapGeneration;

        // Paint the incoming bitmap at opacity 0 first so progressive JPEG
        // bands stay hidden, then fade the layer in.
        _layerUrls[targetLayer] = newUrl;
        _layerSoft[targetLayer] = newSoft;
        await InvokeAsync(StateHasChanged);

        if (!string.IsNullOrWhiteSpace(newUrl))
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("K7.preloadImage", newUrl);
            }
            catch (JSDisconnectedException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }
            catch (JSException)
            {
            }
        }

        if (_disposed || generation != _swapGeneration)
            return;

        _activeLayer = targetLayer;
        await InvokeAsync(StateHasChanged);
    }

    private static bool ShouldShowSubtitle(MediaCardViewModel model)
    {
        if (string.IsNullOrEmpty(model.AdditionalInformations))
            return false;

        // Movies/series often put the year in AdditionalInformations for card footers.
        // The hero already shows ReleaseYear in the meta row.
        if (model.ReleaseYear is { } year
            && string.Equals(model.AdditionalInformations, year.ToString(), StringComparison.Ordinal))
            return false;

        return true;
    }

    private static bool ShouldUseSoftBackdrop(MediaCardViewModel? model)
    {
        if (model is null)
            return false;

        if (model.SoftHeroBackdrop)
            return true;

        // Manual cards (playlists, collections) may omit SoftHeroBackdrop.
        return model.Kind is MediaCardKind.Cover
            || model.MediaType is MediaType.MusicAlbum or MediaType.MusicTrack or MediaType.MusicArtist;
    }

    private static bool ShouldShowRuntime(MediaCardViewModel model) =>
        model.RuntimeMinutes is > 0
        && model.MediaType is MediaType.Movie or MediaType.Serie or MediaType.SerieSeason or MediaType.SerieEpisode;

    private string FormatRuntime(int minutes)
    {
        if (minutes >= 60)
            return S["DurationHoursMinutes", minutes / 60, minutes % 60];

        return S["DurationMinutes", minutes];
    }

    private static string TruncateOverview(string text, int maxLength = 200)
    {
        if (text.Length <= maxLength)
            return text;
        var truncated = text[..maxLength];
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > maxLength / 2)
            truncated = truncated[..lastSpace];
        return truncated + "...";
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _swapGeneration++;
        return ValueTask.CompletedTask;
    }
}
