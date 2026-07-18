using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class AiSmartPlaylistDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    private string _prompt = string.Empty;
    private bool _loading;

    private void Cancel() => Dialog.Close(K7DialogResult.Cancel());

    private async Task Generate()
    {
        if (string.IsNullOrWhiteSpace(_prompt))
            return;

        _loading = true;
        StateHasChanged();

        try
        {
            var trackIds = await MusicIntelligence.CreateSmartPlaylistAsync(_prompt);

            if (trackIds.Count == 0)
            {
                Snackbar.Add(L["NoResults"], K7Severity.Warning);
                _loading = false;
                StateHasChanged();
                return;
            }

            var result = await MediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
            {
                MediaTypes = [MediaType.MusicTrack],
                Ids = trackIds.ToArray(),
                PageNumber = 1,
                PageSize = trackIds.Count
            });

            var tracks = result?.Items?.OfType<LiteMusicTrackDto>()
                .Where(t => t.IndexedFileId.HasValue)
                .Select(t => new AudioQueueItem
                {
                    IndexedFileId = t.IndexedFileId!.Value,
                    MediaId = t.Id,
                    Title = t.Title ?? "Untitled",
                    Artist = t.ArtistName,
                    AlbumTitle = t.AlbumTitle,
                    ArtistId = t.ArtistId,
                    Genre = t.Genre,
                    Duration = t.Duration
                })
                .ToList();

            if (tracks is { Count: > 0 })
            {
                await Audio.PlayTracksAsync(tracks, 0);
                Snackbar.Add(string.Format(L["Playing"], tracks.Count), K7Severity.Info);
            }

            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch
        {
            Snackbar.Add(L["Error"], K7Severity.Error);
            _loading = false;
            StateHasChanged();
        }
    }
}
