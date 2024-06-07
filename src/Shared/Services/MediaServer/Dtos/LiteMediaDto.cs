using AutoMapper;
using MediaClient.Shared.Domain.Models;
using MediaClient.Shared.Services.MediaServer.Mappings;
using System.Text.Json.Serialization;

namespace MediaClient.Shared.Services.MediaServer.Dtos;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LiteMovieDto), "Movie")]
public abstract record LiteMediaDto
{
    public Guid Id { get; init; }
    public string? Title { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public IEnumerable<MetadataPictureDto>? Pictures { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<LiteMediaDto, LiteMedia>()
                .IncludeAllDerived()
                .ForMember(dst => dst.PosterPictureHref, x =>
                {
                    x.PreCondition(src => src.Pictures != null && src.Pictures.Any(x => x.Type == MetadataPictureType.Poster));
                    x.MapFrom<MediaServerBaseUrlPathResolver, MetadataPictureDto>(src => src.Pictures!.First(p => p.Type == MetadataPictureType.Poster));
                })
                .ForMember(dst => dst.BackgroundPictureHref, x =>
                {
                    x.PreCondition(src => src.Pictures != null && src.Pictures.Any(x => x.Type == MetadataPictureType.Backdrop));
                    x.MapFrom<MediaServerBaseUrlPathResolver, MetadataPictureDto>(src => src.Pictures!.First(p => p.Type == MetadataPictureType.Backdrop));
                });

            CreateMap<LiteMovieDto, LiteMovie>();
        }
    }
}

