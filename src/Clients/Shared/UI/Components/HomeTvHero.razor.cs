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

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public MediaCardViewModel? Model { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        var newUrl = Model?.ResolveHeroBackdropUrl();
        var newSoft = ShouldUseSoftBackdrop(Model);
        if (newUrl == _layerUrls[_activeLayer] && newSoft == _layerSoft[_activeLayer])
            return;

        // Soft/sharp is per layer so the outgoing image keeps its blur while opacity fades.
        var targetLayer = 1 - _activeLayer;
        var generation = ++_swapGeneration;

        if (!string.IsNullOrWhiteSpace(newUrl))
        {
            // Wait until the bitmap is decoded so opacity fade does not run on an empty layer
            // (uncached images would otherwise pop in after the transition already finished).
            try
            {
                await JSRuntime.InvokeVoidAsync("K7.preloadImage", newUrl);
            }
            catch (JSDisconnectedException)
            {
                return;
            }
            catch (JSException)
            {
                // Still swap; better a hard cut than a stuck hero.
            }
        }

        if (_disposed || generation != _swapGeneration)
            return;

        _layerUrls[targetLayer] = newUrl;
        _layerSoft[targetLayer] = newSoft;
        _activeLayer = targetLayer;
    }

    private static bool ShouldUseSoftBackdrop(MediaCardViewModel? model) =>
        model?.MediaType is MediaType.MusicAlbum or MediaType.MusicTrack or MediaType.MusicArtist
            or MediaType.SerieEpisode
        || model?.Kind is MediaCardKind.Cover or MediaCardKind.Episode;

    private static string FormatBackdropCss(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "none";

        var escaped = url.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);
        return $"url('{escaped}')";
    }

    private static string FormatRuntime(int minutes)
    {
        if (minutes >= 60)
            return $"{minutes / 60}h {minutes % 60}min";
        return $"{minutes}min";
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
