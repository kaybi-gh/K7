using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
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

        return new AudioQueueItem
        {
            IndexedFileId = indexedFileId,
            MediaId = track.Id,
            Title = track.Title ?? untitledLabel ?? "Untitled",
            Artist = track.ArtistName,
            ArtistId = track.ArtistId,
            AlbumTitle = track.AlbumTitle,
            Genre = track.Genres?.FirstOrDefault(),
            CoverUrl = api.GetAbsoluteUri(cover?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
            CoverDominantColor = cover?.DominantColor,
            Duration = (indexedFile.FileMetadata as AudioFileMetadataDto)?.Duration.TotalSeconds ?? 0,
            Bpm = track.Bpm,
            MusicalKey = track.MusicalKey,
            Energy = track.Energy,
            LoudnessLufs = track.LoudnessLufs,
            FadeInDuration = track.FadeInDuration,
            FadeOutDuration = track.FadeOutDuration,
            ReplayGainTrackGain = track.ReplayGainTrackGain
        };
    }
}
