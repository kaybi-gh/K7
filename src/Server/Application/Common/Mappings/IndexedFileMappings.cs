using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
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
            FileMetadata = domain.FileMetadata?.ToFileMetadataDto()
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
