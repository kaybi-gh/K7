using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Entities.Medias;
using K7.Shared.Dtos.Entities;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Libraries.Queries.GetLibraryPictures;

public record GetLibraryPicturesQuery(Guid LibraryId) : IRequest<IEnumerable<LibraryPictureDto>>;

public class GetLibraryPicturesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetLibraryPicturesQuery, IEnumerable<LibraryPictureDto>>
{
    private static readonly MetadataPictureType[] PickableTypes =
    [
        MetadataPictureType.Poster,
        MetadataPictureType.Backdrop,
        MetadataPictureType.Cover,
        MetadataPictureType.Still
    ];

    public async Task<IEnumerable<LibraryPictureDto>> Handle(GetLibraryPicturesQuery request, CancellationToken cancellationToken)
    {
        var mediaIds = await GetRelatedMediaIdsAsync(request.LibraryId, cancellationToken);
        if (mediaIds.Count == 0)
            return [];

        return await context.MetadataPictures
            .AsNoTracking()
            .Where(p =>
                p.MediaId != null &&
                mediaIds.Contains(p.MediaId.Value) &&
                PickableTypes.Contains(p.Type) &&
                (p.LocalPath != null || p.Variants.Any(v => v.LocalPath != null)))
            .OrderBy(p => p.Type)
            .Take(100)
            .Select(p => new LibraryPictureDto { Id = p.Id, Type = p.Type, DominantColor = p.DominantColor })
            .ToListAsync(cancellationToken);
    }

    private async Task<HashSet<Guid>> GetRelatedMediaIdsAsync(Guid libraryId, CancellationToken cancellationToken)
    {
        var mediaIds = await context.IndexedFiles
            .Where(f => f.LibraryId == libraryId && f.MediaId != null)
            .Select(f => f.MediaId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var result = mediaIds.ToHashSet();
        if (result.Count == 0)
            return result;

        var mediaType = await context.Libraries
            .AsNoTracking()
            .Where(l => l.Id == libraryId)
            .Select(l => (LibraryMediaType?)l.MediaType)
            .FirstOrDefaultAsync(cancellationToken);

        switch (mediaType)
        {
            case LibraryMediaType.Serie:
                var episodeIds = result.ToList();
                var serieIds = await context.Medias.OfType<SerieEpisode>()
                    .Where(e => episodeIds.Contains(e.Id))
                    .Select(e => e.SerieId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                var seasonIds = await context.Medias.OfType<SerieEpisode>()
                    .Where(e => episodeIds.Contains(e.Id))
                    .Select(e => e.SeasonId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                result.UnionWith(serieIds);
                result.UnionWith(seasonIds);
                break;

            case LibraryMediaType.Music:
                var trackIds = result.ToList();
                var albumIds = await context.Medias.OfType<MusicTrack>()
                    .Where(t => trackIds.Contains(t.Id))
                    .Select(t => t.AlbumId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                var artistIds = await context.Medias.OfType<MusicTrack>()
                    .Where(t => trackIds.Contains(t.Id) && t.ArtistId != null)
                    .Select(t => t.ArtistId!.Value)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                result.UnionWith(albumIds);
                result.UnionWith(artistIds);
                break;
        }

        return result;
    }
}
