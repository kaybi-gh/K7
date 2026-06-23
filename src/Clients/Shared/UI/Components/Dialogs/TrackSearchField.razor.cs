using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class TrackSearchField
{
    [Parameter] public string Label { get; set; } = string.Empty;
    [Parameter] public string Placeholder { get; set; } = string.Empty;
    [Parameter] public LiteMusicTrackDto? SelectedTrack { get; set; }
    [Parameter] public EventCallback<LiteMusicTrackDto?> SelectedTrackChanged { get; set; }

    [Inject] private IMediaService MediaService { get; set; } = default!;

    private string _query = string.Empty;
    private bool _loading;
    private List<LiteMusicTrackDto> _results = [];
    private CancellationTokenSource? _searchCts;

    private async Task OnDebouncedSearch(string? value)
    {
        _query = value ?? string.Empty;
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        if (string.IsNullOrWhiteSpace(_query))
        {
            _results = [];
            _loading = false;
            await InvokeAsync(StateHasChanged);
            return;
        }

        _loading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            await Task.Delay(300, token);
            var result = await MediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
            {
                MediaTypes = [MediaType.MusicTrack],
                SearchText = _query.Trim(),
                PageNumber = 1,
                PageSize = 12
            }, token);

            _results = result?.Items?.OfType<LiteMusicTrackDto>()
                .Where(t => t.IndexedFileId.HasValue)
                .ToList() ?? [];
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                _loading = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task SelectTrack(LiteMusicTrackDto track)
    {
        SelectedTrack = track;
        _query = track.Title ?? string.Empty;
        _results = [];
        await SelectedTrackChanged.InvokeAsync(track);
    }
}
