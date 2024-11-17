using AutoMapper;
using K7.Clients.Shared.Domain.Models;
using K7.Clients.Shared.Services.MediaServer.Mappings;

namespace K7.Clients.Shared.Services.MediaServer.Dtos;

public record MovieDto : MediaDto
{
    public string? TagLine { get; init; }
    public string? Overview { get; init; }
    public string? OriginalLanguage { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<MovieDto, Movie>()
                .ForMember(dst => dst.Id, x => x.MapFrom(src => src.Id))
                .ForMember(dst => dst.PosterPictureHref, x =>
                {
                    x.PreCondition(src => src.Pictures != null && src.Pictures.Any(x => x.Type == MetadataPictureType.Poster));
                    x.MapFrom<MediaServerAbsoluteUriResolver, Uri?>(src => src.Pictures!.First(p => p.Type == MetadataPictureType.Poster).Uri);
                })
                .ForMember(dst => dst.BackgroundPictureHref, x =>
                {
                    x.PreCondition(src => src.Pictures != null && src.Pictures.Any(x => x.Type == MetadataPictureType.Backdrop));
                    x.MapFrom<MediaServerAbsoluteUriResolver, Uri?>(src => src.Pictures!.First(p => p.Type == MetadataPictureType.Backdrop).Uri);
                })
                .ForMember(dst => dst.Synopsis, x => x.MapFrom(src => src.Overview))
                .ForMember(dst => dst.Casting, x => x.MapFrom(src => src.PersonRoles))
                .ForMember(dst => dst.Genres, x => x.MapFrom(src => src.Genres))
                .ForMember(dst => dst.Sources, x => x.MapFrom<MediaServerAbsoluteUriListResolver, IEnumerable<Uri?>?>(src => src.IndexedFiles!.Select(x => x.HlsStreamUri)))
                .ForMember(dst => dst.Watched, x => x.Ignore())
                .ForMember(dst => dst.Progress, x => x.Ignore())
                .ForMember(dst => dst.Rating, x => x.Ignore())
                .ForMember(dst => dst.AdditionalInformations, x => x.Ignore());
        }
    }
}
