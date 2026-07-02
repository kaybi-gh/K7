using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities.Collections;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Server.Application.Common.Mappings;

public static class CollectionMappings
{
    extension(Collection domain)
    {
        public CollectionDto ToCollectionDto() => new()
        {
            Id = domain.Id,
            Title = domain.Title,
            Description = domain.Description,
            IsPublic = domain.IsPublic,
            UserId = domain.UserId,
            MediaType = domain.MediaType,
            CoverPicture = domain.CoverPicture?.ToMetadataPictureDto(),
            ItemCount = domain.Items.Count,
            Created = domain.Created,
            LastModified = domain.LastModified
        };

        public LiteCollectionDto ToLiteCollectionDto() => new()
        {
            Id = domain.Id,
            Title = domain.Title,
            Description = domain.Description,
            IsPublic = domain.IsPublic,
            UserId = domain.UserId,
            MediaType = domain.MediaType,
            CoverPicture = domain.CoverPicture?.ToMetadataPictureDto(),
            PreviewPictures = domain.ToPreviewPictureDtos(),
            ItemCount = domain.Items.Count,
            Created = domain.Created,
            LastModified = domain.LastModified
        };
    }

    extension(CollectionItem domain)
    {
        public CollectionItemDto ToCollectionItemDto() => new()
        {
            Id = domain.Id,
            CollectionId = domain.CollectionId,
            Order = domain.Order,
            Media = domain.Media.ToLiteMediaDto()
        };
    }
}
