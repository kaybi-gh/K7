using K7.Server.Application.Common.Models.Dtos;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Common.Converters;

public static class MediaDtoConverter
{
    public static MediaDto ConvertToDto(this BaseMedia media, IMapper mapper)
    {
        return media.Type switch
        {
            MediaType.Movie => mapper.Map<MovieDto>(media),
            MediaType.MusicAlbum => throw new NotImplementedException(),
            MediaType.MusicTrack => throw new NotImplementedException(),
            MediaType.Serie => throw new NotImplementedException(),
            MediaType.SerieEpisode => throw new NotImplementedException(),
            MediaType.SerieSeason => throw new NotImplementedException(),
            _ => throw new InvalidOperationException($"Unsupported media type: {media.Type}")
        };
    }

    public static LiteMediaDto ConvertToLiteDto(this BaseMedia media, IMapper mapper)
    {
        return media.Type switch
        {
            MediaType.Movie => mapper.Map<LiteMovieDto>(media),
            MediaType.MusicAlbum => throw new NotImplementedException(),
            MediaType.MusicTrack => throw new NotImplementedException(),
            MediaType.Serie => throw new NotImplementedException(),
            MediaType.SerieEpisode => throw new NotImplementedException(),
            MediaType.SerieSeason => throw new NotImplementedException(),
            _ => throw new InvalidOperationException($"Unsupported media type: {media.Type}")
        };
    }
}
