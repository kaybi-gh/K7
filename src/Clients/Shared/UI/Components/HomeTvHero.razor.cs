using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class HomeTvHero
{
    private readonly string?[] _layerUrls = [null, null];
    private int _activeLayer;

    [Parameter] public MediaCardViewModel? Model { get; set; }

    private bool UseSoftBackdrop =>
        Model?.MediaType is MediaType.MusicAlbum or MediaType.MusicTrack or MediaType.MusicArtist
            or MediaType.SerieEpisode
        || Model?.Kind is MediaCardKind.Cover or MediaCardKind.Episode;

    protected override void OnParametersSet()
    {
        var newUrl = Model?.ResolveHeroBackdropUrl();
        if (newUrl == _layerUrls[_activeLayer])
            return;

        // Put the new image on the inactive layer, then make it active
        var inactiveLayer = 1 - _activeLayer;
        _layerUrls[inactiveLayer] = newUrl;
        _activeLayer = inactiveLayer;
    }

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
}
