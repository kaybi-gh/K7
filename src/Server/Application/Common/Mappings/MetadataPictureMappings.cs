using K7.Server.Domain.Entities;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Common.Mappings;

public static class MetadataPictureMappings
{
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
}
