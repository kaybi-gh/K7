using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.Music.Queries.GetMusicStats;

public record GetMusicStatsQuery : IRequest<MusicStatsDto>;

public class GetMusicStatsQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetMusicStatsQuery, MusicStatsDto>
{
    public async Task<MusicStatsDto> Handle(GetMusicStatsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return new MusicStatsDto();

        // Completed music sessions for this user
        var completedSessions = context.MediaPlaybackSessions
            .Where(s => s.UserId == userId && s.CompletedAt != null)
            .Join(
                context.Medias.Where(m => m.Type == MediaType.MusicTrack),
                s => s.MediaId,
                m => m.Id,
                (s, m) => new { s.MediaId, s.DurationSeconds, s.CompletedAt, Media = m });

        var totalPlays = await completedSessions.CountAsync(cancellationToken);
        var totalSeconds = await completedSessions.SumAsync(s => s.DurationSeconds, cancellationToken);
        var uniqueTracks = await completedSessions.Select(s => s.MediaId).Distinct().CountAsync(cancellationToken);

        // Top tracks by play count
        var topTracks = await completedSessions
            .GroupBy(s => new { s.MediaId, s.Media.Title })
            .Select(g => new TopItemDto
            {
                Id = g.Key.MediaId,
                Name = g.Key.Title ?? "Sans titre",
                PlayCount = g.Count()
            })
            .OrderByDescending(x => x.PlayCount)
            .Take(10)
            .ToListAsync(cancellationToken);

        // Top artists: join through PersonRoles
        var topArtists = await completedSessions
            .Join(
                context.PersonRoles.OfType<MusicArtist>(),
                s => s.MediaId,
                pr => pr.MediaId,
                (s, pr) => new { pr.PersonId, pr.Person.Name })
            .GroupBy(x => new { x.PersonId, x.Name })
            .Select(g => new TopItemDto
            {
                Id = g.Key.PersonId,
                Name = g.Key.Name ?? "Artiste inconnu",
                PlayCount = g.Count()
            })
            .OrderByDescending(x => x.PlayCount)
            .Take(10)
            .ToListAsync(cancellationToken);

        // Top albums: join through MusicTrack.AlbumId
        var topAlbums = await completedSessions
            .Join(
                context.Medias.OfType<MusicTrack>(),
                s => s.MediaId,
                t => t.Id,
                (s, t) => new { t.AlbumId })
            .Join(
                context.Medias.OfType<MusicAlbum>(),
                x => x.AlbumId,
                a => a.Id,
                (x, a) => new { a.Id, a.Title })
            .GroupBy(x => new { x.Id, x.Title })
            .Select(g => new TopItemDto
            {
                Id = g.Key.Id,
                Name = g.Key.Title ?? "Album inconnu",
                PlayCount = g.Count()
            })
            .OrderByDescending(x => x.PlayCount)
            .Take(10)
            .ToListAsync(cancellationToken);

        // Top genres
        var topGenres = await completedSessions
            .SelectMany(s => s.Media.Genres, (s, genre) => genre)
            .GroupBy(g => g)
            .Select(g => new GenreStatDto
            {
                Genre = g.Key,
                PlayCount = g.Count()
            })
            .OrderByDescending(x => x.PlayCount)
            .Take(10)
            .ToListAsync(cancellationToken);

        return new MusicStatsDto
        {
            TotalListeningHours = Math.Round(totalSeconds / 3600.0, 1),
            TotalCompletedPlays = totalPlays,
            UniqueTracksPlayed = uniqueTracks,
            TopArtists = topArtists,
            TopAlbums = topAlbums,
            TopTracks = topTracks,
            TopGenres = topGenres
        };
    }
}
