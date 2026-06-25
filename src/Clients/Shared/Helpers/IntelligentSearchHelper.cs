using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Helpers;

public static class IntelligentSearchHelper
{
    public const int DefaultResultCount = 50;

    public static async Task<List<Guid>> SearchTrackIdsAsync(
        IMusicIntelligenceClientService musicIntelligence,
        IntelligentSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        return request.Kind switch
        {
            IntelligentSearchKind.Sonic => await musicIntelligence.SearchTracksBySonicTextAsync(
                request.Query, DefaultResultCount, cancellationToken),
            IntelligentSearchKind.Lyrics => await musicIntelligence.SearchTracksByLyricsAsync(
                request.Query, DefaultResultCount, cancellationToken),
            _ => []
        };
    }

    public static async Task<List<LiteMusicTrackDto>> LoadScopedTracksAsync(
        IMediaService mediaService,
        IReadOnlyList<Guid> trackIds,
        Guid[]? libraryIds,
        Guid[]? libraryGroupIds,
        CancellationToken cancellationToken = default)
    {
        if (trackIds.Count == 0)
            return [];

        var result = await mediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            LibraryIds = libraryIds,
            LibraryGroupIds = libraryGroupIds,
            MediaTypes = [MediaType.MusicTrack],
            Ids = trackIds.ToArray(),
            PageNumber = 1,
            PageSize = trackIds.Count
        }, cancellationToken);

        var trackMap = result?.Items?
            .OfType<LiteMusicTrackDto>()
            .Where(t => t.IndexedFileId.HasValue)
            .ToDictionary(t => t.Id) ?? [];

        return trackIds
            .Where(trackMap.ContainsKey)
            .Select(id => trackMap[id])
            .ToList();
    }

    public static List<AudioQueueItem> ToQueueItems(IEnumerable<LiteMusicTrackDto> tracks, string untitledLabel) =>
        tracks.Select(t => new AudioQueueItem
        {
            IndexedFileId = t.IndexedFileId!.Value,
            MediaId = t.Id,
            Title = t.Title ?? untitledLabel,
            Artist = t.ArtistName,
            ArtistId = t.ArtistId,
            AlbumTitle = t.AlbumTitle,
            Genre = t.Genre,
            Duration = t.Duration
        }).ToList();
}
