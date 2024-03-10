using MediaServer.Domain.Entities.Medias;

namespace MediaServer.Application.Features.Medias.Queries.GetMedias;

public record MediaDto
{
    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<BaseMedia, MediaDto>();
        }
    }
}
