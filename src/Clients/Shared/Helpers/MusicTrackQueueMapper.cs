using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Helpers;

public static class MusicTrackQueueMapper
{
    public static AudioQueueItem? ToQueueItem(
        MusicTrackDto track,
        IK7ServerService api,
        string? untitledLabel = null)
    {
        var indexedFile = track.IndexedFiles?.FirstOrDefault();
        if (indexedFile?.Id is not { } indexedFileId)
            return null;

        var cover = track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
            ?? track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster);

        return CreateQueueItem(
            track.Id,
            indexedFileId,
            track.Title,
            track.ArtistName,
            track.ArtistId,
            track.AlbumTitle,
            track.Genres?.FirstOrDefault(),
            cover,
            api,
            (indexedFile.FileMetadata as AudioFileMetadataDto)?.Duration.TotalSeconds ?? 0,
            track.LoudnessLufs,
            track.FadeInDuration,
            track.FadeOutDuration,
            track.ReplayGainTrackGain,
            untitledLabel);
    }

    public static AudioQueueItem? ToQueueItem(
        LiteMusicTrackDto track,
        IK7ServerService api,
        string? untitledLabel = null)
    {
        if (track.IndexedFileId is not { } indexedFileId)
            return null;

        var cover = track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
            ?? track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster);

        return CreateQueueItem(
            track.Id,
            indexedFileId,
            track.Title,
            track.ArtistName,
            track.ArtistId,
            track.AlbumTitle,
            track.Genre,
            cover,
            api,
            track.Duration ?? 0,
            track.LoudnessLufs,
            track.FadeInDuration,
            track.FadeOutDuration,
            track.ReplayGainTrackGain,
            untitledLabel);
    }

    public static List<AudioQueueItem> ToQueueItems(
        IEnumerable<LiteMusicTrackDto> tracks,
        IK7ServerService api,
        string? untitledLabel = null) =>
        tracks
            .Select(t => ToQueueItem(t, api, untitledLabel))
            .Where(t => t is not null)
            .Cast<AudioQueueItem>()
            .ToList();

    private static AudioQueueItem CreateQueueItem(
        Guid mediaId,
        Guid indexedFileId,
        string? title,
        string? artistName,
        Guid? artistId,
        string? albumTitle,
        string? genre,
        MetadataPictureDto? cover,
        IK7ServerService api,
        double duration,
        double? loudnessLufs,
        double? fadeInDuration,
        double? fadeOutDuration,
        double? replayGainTrackGain,
        string? untitledLabel) => new()
    {
        IndexedFileId = indexedFileId,
        MediaId = mediaId,
        Title = title ?? untitledLabel ?? "Untitled",
        Artist = artistName,
        ArtistId = artistId,
        AlbumTitle = albumTitle,
        Genre = genre,
        CoverUrl = api.GetAbsoluteUri(cover?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
        CoverDominantColor = cover?.DominantColor,
        Duration = duration,
        LoudnessLufs = loudnessLufs,
        FadeInDuration = fadeInDuration,
        FadeOutDuration = fadeOutDuration,
        ReplayGainTrackGain = replayGainTrackGain
    };
}
