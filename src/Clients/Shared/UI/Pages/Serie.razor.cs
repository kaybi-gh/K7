using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Clients.Shared.UI.Components.Dialogs;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class Serie
{
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Parameter]
    public required string Id { get; set; }

    private SerieDto? _serie;
    private string? _posterUrl;
    private string? _backdropUrl;
    private string? _logoUrl;
    private List<LiteSerieSeasonDto> _seasons = [];
    private List<MediaCardViewModel> _similarMedia = [];
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

        _ = LoadSimilarMediaAsync();
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

        var parameters = new K7DialogParameters<ReIdentifyDialog>
        {
            { x => x.MediaId, _serie.Id },
            { x => x.InitialSearchQuery, _serie.Title },
            { x => x.MediaType, K7.Server.Domain.Enums.MediaType.Serie }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ReIdentifyDialog>(L["ReIdentifyMediaDialogTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add(L["ReIdentifyMediaSent"], K7Severity.Success);
            NavigationManager.NavigateTo("/");
        }
    }

    private async Task RefreshMetadataAsync()
    {
        if (_serie is null) return;

        try
        {
            await k7ServerService.RefreshMediaMetadataAsync(_serie.Id);
            Snackbar.Add(L["RefreshMetadataSent"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task OpenEditMetadataDialogAsync()
    {
        if (_serie is null) return;

        var parameters = new K7DialogParameters<EditMetadataDialog>
        {
            { x => x.Media, _serie }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<EditMetadataDialog>(L["EditMetadata"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            var media = await k7ServerService.GetMediaAsync(Guid.Parse(Id));
            if (media is SerieDto serie)
            {
                _serie = serie;
                StateHasChanged();
            }
        }
    }

    private Task OpenSynopsisDialogAsync()
    {
        if (_serie is null || string.IsNullOrWhiteSpace(_serie.Overview)) return Task.CompletedTask;

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
        var parameters = new K7DialogParameters
        {
            { "ContentText", _serie.Overview },
            { "ButtonText", S["Cancel"].Value }
        };
        return DialogService.ShowAsync<OverviewDialog>(L["Overview"], parameters, options);
    }

    private Task OpenTrailerAsync()
    {
        if (_serie?.Trailers is not { Count: > 0 }) return Task.CompletedTask;

        var trailer = _serie.Trailers.FirstOrDefault(t => t.Type == "Trailer") ?? _serie.Trailers[0];
        var parameters = new K7DialogParameters<TrailerDialog>
        {
            { x => x.TrailerKey, trailer.Key },
            { x => x.TrailerSite, trailer.Site ?? "YouTube" }
        };
        var options = new K7DialogOptions { FullScreen = true, CloseOnEscapeKey = true, CloseButton = true };
        return DialogService.ShowAsync<TrailerDialog>(trailer.Name ?? L["Trailer"], parameters, options);
    }

    private void NavigateToStudio(string studio)
    {
        NavigationManager.NavigateTo($"/search?studio={Uri.EscapeDataString(studio)}");
    }

    private async Task LoadSimilarMediaAsync()
    {
        if (_serie is null) return;

        try
        {
            var similar = await k7ServerService.GetSimilarMediaAsync(_serie.Id);
            _similarMedia = similar.Select(m => new MediaCardViewModel
            {
                Id = m.Id.ToString(),
                Kind = MediaCardKind.Serie,
                Title = m.Title,
                AdditionalInformations = m.ReleaseDate?.Year.ToString(),
                PictureUrl = apiClient.GetAbsoluteUri(
                    m.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
                        ?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
            }).ToList();
            await InvokeAsync(StateHasChanged);
        }
        catch
        {
            // Non-critical - silently ignore if similar media fails
        }
    }
}
