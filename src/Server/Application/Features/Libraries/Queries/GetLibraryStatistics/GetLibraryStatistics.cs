using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Features.Libraries.Queries.GetLibraryStatistics;

[Authorize(Roles = Roles.Administrator)]
public record GetLibraryStatisticsQuery : IRequest<List<LibraryStatisticsDto>>;

public class GetLibraryStatisticsQueryHandler : IRequestHandler<GetLibraryStatisticsQuery, List<LibraryStatisticsDto>>
{
    private readonly IApplicationDbContext _context;

    public GetLibraryStatisticsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<LibraryStatisticsDto>> Handle(GetLibraryStatisticsQuery request, CancellationToken cancellationToken)
    {
        // Direct media counts (leaf media linked to indexed files: tracks, episodes, movies)
        var directCounts = await _context.IndexedFiles
            .Where(f => f.MediaId != null)
            .Select(f => new { f.LibraryId, MediaId = f.MediaId!.Value, f.Media!.Type })
            .Distinct()
            .GroupBy(x => new { x.LibraryId, x.Type })
            .Select(g => new { g.Key.LibraryId, g.Key.Type, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Music albums (distinct albums from tracks in each library)
        var albumCounts = await _context.Medias
            .OfType<MusicTrack>()
            .SelectMany(t => t.IndexedFiles.Select(f => new { f.LibraryId, t.AlbumId }))
            .GroupBy(x => x.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Select(x => x.AlbumId).Distinct().Count() })
            .ToListAsync(cancellationToken);

        // Music artists (distinct artists from albums of tracks in each library)
        var artistCounts = await _context.Medias
            .OfType<MusicTrack>()
            .Where(t => t.Album.ArtistId != null)
            .SelectMany(t => t.IndexedFiles.Select(f => new { f.LibraryId, t.Album.ArtistId }))
            .GroupBy(x => x.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Select(x => x.ArtistId).Distinct().Count() })
            .ToListAsync(cancellationToken);

        // Series (distinct series from episodes in each library)
        var serieCounts = await _context.Medias
            .OfType<SerieEpisode>()
            .SelectMany(e => e.IndexedFiles.Select(f => new { f.LibraryId, e.SerieId }))
            .GroupBy(x => x.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Select(x => x.SerieId).Distinct().Count() })
            .ToListAsync(cancellationToken);

        // Seasons (distinct seasons from episodes in each library)
        var seasonCounts = await _context.Medias
            .OfType<SerieEpisode>()
            .SelectMany(e => e.IndexedFiles.Select(f => new { f.LibraryId, e.SeasonId }))
            .GroupBy(x => x.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Select(x => x.SeasonId).Distinct().Count() })
            .ToListAsync(cancellationToken);

        // File counts
        var fileCounts = await _context.IndexedFiles
            .GroupBy(f => f.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var libraryIds = await _context.Libraries
            .AsNoTracking()
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        return libraryIds.Select(id =>
        {
            var mediaCounts = directCounts
                .Where(m => m.LibraryId == id)
                .ToDictionary(m => m.Type, m => m.Count);

            var albums = albumCounts.FirstOrDefault(x => x.LibraryId == id)?.Count ?? 0;
            if (albums > 0)
                mediaCounts[MediaType.MusicAlbum] = albums;

            var artists = artistCounts.FirstOrDefault(x => x.LibraryId == id)?.Count ?? 0;
            if (artists > 0)
                mediaCounts[MediaType.MusicArtist] = artists;

            var series = serieCounts.FirstOrDefault(x => x.LibraryId == id)?.Count ?? 0;
            if (series > 0)
                mediaCounts[MediaType.Serie] = series;

            var seasons = seasonCounts.FirstOrDefault(x => x.LibraryId == id)?.Count ?? 0;
            if (seasons > 0)
                mediaCounts[MediaType.SerieSeason] = seasons;

            return new LibraryStatisticsDto
            {
                LibraryId = id,
                FileCount = fileCounts.FirstOrDefault(f => f.LibraryId == id)?.Count ?? 0,
                MediaCounts = mediaCounts
            };
        }).ToList();
    }
}
