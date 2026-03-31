using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class Serie
{
    [Parameter]
    public required string Id { get; set; }

    private SerieDto? _serie;
    private string? _posterUrl;
    private string? _backdropUrl;
    private string? _logoUrl;
    private List<LiteSerieSeasonDto> _seasons = [];
    private bool _loading = true;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;

        var media = await k7ServerService.GetMediaAsync(Guid.Parse(Id));
        if (media is SerieDto serie)
        {
            _serie = serie;

            _posterUrl = apiClient.GetAbsoluteUri(
                serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?
                    .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;

            _backdropUrl = apiClient.GetAbsoluteUri(
                serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop)?
                    .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;

            _logoUrl = apiClient.GetAbsoluteUri(
                serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Logo)?
                    .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;

            _seasons = (serie.Seasons ?? [])
                .OrderBy(s => s.SeasonNumber == 0 ? int.MaxValue : s.SeasonNumber)
                .ToList();
        }

        _loading = false;
    }

    private string? GetSeasonPosterUrl(LiteSerieSeasonDto season)
    {
        return apiClient.GetAbsoluteUri(
            season.Poster?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;
    }

    private void NavigateToSeason(LiteSerieSeasonDto season)
    {
        NavigationManager.NavigateTo($"/series/{Id}/seasons/{season.SeasonNumber}");
    }
}
