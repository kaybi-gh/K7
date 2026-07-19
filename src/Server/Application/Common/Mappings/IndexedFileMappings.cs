using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Models;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Common.Mappings;

public static class IndexedFileMappings
{
    extension(IndexedFile domain)
    {
        public IndexedFileDto ToIndexedFileDto() => new()
        {
            Id = domain.Id,
            Name = domain.Name,
            LibraryId = domain.LibraryId,
            Extension = domain.Extension,
            Path = domain.Path,
            ParentDirectory = domain.ParentDirectory,
            Hash = domain.Hash,
            Size = domain.Size,
            Identification = domain.Identification?.ToMediaIdentificationDto(),
            FileMetadata = domain.FileMetadata?.ToFileMetadataDto()
        };
    }

    extension(MediaIdentification domain)
    {
        public MediaIdentificationDto ToMediaIdentificationDto() => new()
        {
            Title = domain.Title,
            ReleaseYear = domain.ReleaseYear,
            TrackNumber = domain.TrackNumber,
            AlbumName = domain.AlbumName,
            ArtistName = domain.ArtistName,
            SeriesTitle = domain.SeriesTitle,
            SeasonNumber = domain.SeasonNumber,
            EpisodeNumber = domain.EpisodeNumber,
            AbsoluteNumber = domain.AbsoluteNumber
        };
    }

    extension(RemoteIndexedFile domain)
    {
        public RemoteIndexedFileDto ToRemoteIndexedFileDto() => new()
        {
            Id = domain.Id,
            PeerServerId = domain.PeerServerId,
            RemoteFileId = domain.RemoteFileId,
            Name = domain.Name,
            Extension = domain.Extension,
            Size = domain.Size,
            RemoteMediaId = domain.RemoteMediaId,
            Container = domain.Container,
            Duration = domain.Duration,
            VideoBitrate = domain.VideoBitrate,
            VideoResolution = domain.VideoResolution
        };
    }
}
