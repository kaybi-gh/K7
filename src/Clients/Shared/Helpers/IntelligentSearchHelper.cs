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

        var tracks = await LoadTracksByIdsAsync(
            mediaService, trackIds, libraryIds, libraryGroupIds, cancellationToken);

        // AudioMuse may return ids that are in the catalogue but outside the current
        // browse scope filter; fall back to a global id lookup so search still plays.
        if (tracks.Count == 0 && (libraryIds is { Length: > 0 } || libraryGroupIds is { Length: > 0 }))
        {
            tracks = await LoadTracksByIdsAsync(
                mediaService, trackIds, libraryIds: null, libraryGroupIds: null, cancellationToken);
        }

        return tracks;
    }

    private static async Task<List<LiteMusicTrackDto>> LoadTracksByIdsAsync(
        IMediaService mediaService,
        IReadOnlyList<Guid> trackIds,
        Guid[]? libraryIds,
        Guid[]? libraryGroupIds,
        CancellationToken cancellationToken)
    {
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

    public static List<AudioQueueItem> ToQueueItems(
        IEnumerable<LiteMusicTrackDto> tracks,
        IK7ServerService api,
        string? untitledLabel = null) =>
        MusicTrackQueueMapper.ToQueueItems(tracks, api, untitledLabel);
}
