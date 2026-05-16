using K7.Server.Domain.Entities.Medias;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Server.Application.Common.Mappings;

public static class MediaSegmentMappings
{
    extension(MediaSegment domain)
    {
        public MediaSegmentDto ToMediaSegmentDto() => new()
        {
            Type = (K7.Shared.Enums.MediaSegmentType)domain.Type,
            StartMs = domain.StartMs,
            EndMs = domain.EndMs
        };
    }
}
