using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Features.MediaPictures.Queries.GetMediaPicture;

public record PictureDto
{
    public int Id { get; init; }
    public MediaPictureType? Type { get; init; }
    public Uri? Uri { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<MediaPicture, PictureDto>()
                .ForMember(dest => dest.Uri, x => x.MapFrom(src => new Uri($"/api/pictures/{src.Id}", UriKind.Relative)));
        }
    }
}
