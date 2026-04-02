using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Clients.Shared.UI.Components.Dialogs;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Pages;

public partial class Serie
{
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
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

    private void WatchAsync()
    {
        var first = _seasons.FirstOrDefault();
        if (first is not null)
            NavigationManager.NavigateTo($"/series/{Id}/seasons/{first.SeasonNumber}");
    }

    private async Task OpenMediaReIdentifyDialogAsync()
    {
        if (_serie is null) return;

        var parameters = new DialogParameters<ReIdentifyDialog>
        {
            { x => x.MediaId, _serie.Id },
            { x => x.InitialSearchQuery, _serie.Title },
            { x => x.MediaType, K7.Server.Domain.Enums.MediaType.Serie }
        };

        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ReIdentifyDialog>(L["ReIdentifyMediaDialogTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add(L["ReIdentifyMediaSent"], Severity.Success);
            NavigationManager.NavigateTo("/");
        }
    }
}
