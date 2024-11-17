using AutoMapper;
using K7.Clients.Shared.Domain.Models;
using K7.Clients.Shared.Services.MediaServer.Mappings;
using System.Text.Json.Serialization;

namespace K7.Clients.Shared.Services.MediaServer.Dtos;

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
                    x.MapFrom<MediaServerAbsoluteUriResolver, Uri?>(src => src.Pictures!.First(p => p.Type == MetadataPictureType.Poster).Uri);
                })
                .ForMember(dst => dst.BackgroundPictureHref, x =>
                {
                    x.PreCondition(src => src.Pictures != null && src.Pictures.Any(x => x.Type == MetadataPictureType.Backdrop));
                    x.MapFrom<MediaServerAbsoluteUriResolver, Uri?>(src => src.Pictures!.First(p => p.Type == MetadataPictureType.Backdrop).Uri);
                });

            CreateMap<LiteMovieDto, LiteMovie>();
        }
    }
}

