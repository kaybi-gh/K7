using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Common.Mappings;

public static class EntityDtoMappings
{
    extension(Library domain)
    {
        public LibraryDto ToLibraryDto() => new()
        {
            Id = domain.Id,
            Title = domain.Title,
            MediaType = domain.MediaType,
            RootPath = domain.RootPath
        };
    }

    extension(BackgroundTask domain)
    {
        public BackgroundTaskDto ToBackgroundTaskDto() => new()
        {
            Id = domain.Id,
            Name = domain.Name,
            TargetEntityType = domain.TargetEntityType,
            TargetEntityId = domain.TargetEntityId,
            Status = domain.Status,
            Priority = domain.Priority,
            RetryCount = domain.RetryCount,
            MaxRetryCount = domain.MaxRetryCount
        };
    }

    extension(ExternalId domain)
    {
        public ExternalIdDto ToExternalIdDto() => new()
        {
            Id = domain.Id,
            ProviderName = domain.ProviderName,
            Value = domain.Value
        };
    }

    extension(MetadataPicture domain)
    {
        public MetadataPictureDto ToMetadataPictureDto() => new()
        {
            Id = domain.Id,
            Type = domain.Type,
            Uri = new Uri($"/api/metadata-pictures/{domain.Id}", UriKind.Relative),
            DominantColor = domain.DominantColor,
            AvailableSizes = domain.Variants.Select(v => v.Size).ToList()
        };
    }

    extension(UserMediaState domain)
    {
        public UserMediaStateDto ToUserMediaStateDto() => new()
        {
            LastPlaybackPosition = domain.LastPlaybackPosition,
            ProgressPercentage = domain.ProgressPercentage,
            IsCompleted = domain.IsCompleted,
            PlayCount = domain.PlayCount,
            LastInteractedAt = domain.LastInteractedAt
        };
    }

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
            IsSplitPart = domain.IsSplitPart,
            IsComposite = domain.IsComposite,
            FileMetadata = domain.FileMetadata?.ToFileMetadataDto()
        };
    }
}
