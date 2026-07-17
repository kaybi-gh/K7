using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class SonicPathDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    private LiteMusicTrackDto? _startTrack;
    private LiteMusicTrackDto? _endTrack;
    private bool _loading;

    private void Cancel() => Dialog.Close(K7DialogResult.Cancel());

    private async Task PlayAsync()
    {
        if (_startTrack is null || _endTrack is null)
            return;

        _loading = true;
        try
        {
            var trackIds = await MusicIntelligence.GetSonicPathAsync(_startTrack.Id, _endTrack.Id);
            if (trackIds.Count == 0)
            {
                Snackbar.Add(L["NoPath"], K7Severity.Warning);
                return;
            }

            var result = await MediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
            {
                MediaTypes = [MediaType.MusicTrack],
                Ids = trackIds.ToArray(),
                PageNumber = 1,
                PageSize = trackIds.Count
            });

            var trackMap = result?.Items?.OfType<LiteMusicTrackDto>()
                .Where(t => t.IndexedFileId.HasValue)
                .ToDictionary(t => t.Id) ?? [];

            var ordered = trackIds
                .Where(trackMap.ContainsKey)
                .Select(id => ToQueueItem(trackMap[id]))
                .ToList();

            if (ordered.Count == 0)
            {
                Snackbar.Add(L["NoPath"], K7Severity.Warning);
                return;
            }

            await Audio.PlayTracksAsync(ordered, 0);
            Snackbar.Add(string.Format(L["PlayingPath"], ordered.Count), K7Severity.Info);
            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch
        {
            Snackbar.Add(L["Error"], K7Severity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private AudioQueueItem ToQueueItem(LiteMusicTrackDto t) => new()
    {
        IndexedFileId = t.IndexedFileId!.Value,
        MediaId = t.Id,
        Title = t.Title ?? S["Untitled"],
        Artist = t.ArtistName,
        ArtistId = t.ArtistId,
        AlbumTitle = t.AlbumTitle,
        Genre = t.Genre,
        Duration = t.Duration
    };
}
