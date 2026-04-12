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

        // Play counts from UserMediaStates (authoritative aggregate: imports + native plays)
        var musicStates = context.UserMediaStates
            .Where(s => s.UserId == userId && s.PlayCount > 0)
            .Join(
                context.Medias.Where(m => m.Type == MediaType.MusicTrack),
                s => s.MediaId,
                m => m.Id,
                (s, m) => new { s.MediaId, s.PlayCount, Media = m });

        var totalPlays = await musicStates.SumAsync(s => s.PlayCount, cancellationToken);
        var uniqueTracks = await musicStates.CountAsync(cancellationToken);

        // Listening time from completed sessions (only sessions have actual durations)
        var totalSeconds = await context.MediaPlaybackSessions
            .Where(s => s.UserId == userId && s.CompletedAt != null)
            .Join(
                context.Medias.Where(m => m.Type == MediaType.MusicTrack),
                s => s.MediaId,
                m => m.Id,
                (s, _) => s.DurationSeconds)
            .SumAsync(cancellationToken);

        // Top tracks by play count
        var topTracks = await musicStates
            .Select(s => new TopItemDto
            {
                Id = s.MediaId,
                Name = s.Media.Title ?? "Sans titre",
                PlayCount = s.PlayCount
            })
            .OrderByDescending(x => x.PlayCount)
            .Take(10)
            .ToListAsync(cancellationToken);

        // Top artists: join through PersonRoles
        var topArtists = await musicStates
            .Join(
                context.PersonRoles.OfType<MusicArtist>(),
                s => s.MediaId,
                pr => pr.MediaId,
                (s, pr) => new { s.PlayCount, pr.PersonId, pr.Person.Name })
            .GroupBy(x => new { x.PersonId, x.Name })
            .Select(g => new TopItemDto
            {
                Id = g.Key.PersonId,
                Name = g.Key.Name ?? "Artiste inconnu",
                PlayCount = g.Sum(x => x.PlayCount)
            })
            .OrderByDescending(x => x.PlayCount)
            .Take(10)
            .ToListAsync(cancellationToken);

        // Top albums: join through MusicTrack.AlbumId
        var topAlbums = await musicStates
            .Join(
                context.Medias.OfType<MusicTrack>(),
                s => s.MediaId,
                t => t.Id,
                (s, t) => new { s.PlayCount, t.AlbumId })
            .Join(
                context.Medias.OfType<MusicAlbum>(),
                x => x.AlbumId,
                a => a.Id,
                (x, a) => new { x.PlayCount, a.Id, a.Title })
            .GroupBy(x => new { x.Id, x.Title })
            .Select(g => new TopItemDto
            {
                Id = g.Key.Id,
                Name = g.Key.Title ?? "Album inconnu",
                PlayCount = g.Sum(x => x.PlayCount)
            })
            .OrderByDescending(x => x.PlayCount)
            .Take(10)
            .ToListAsync(cancellationToken);

        // Top genres
        var topGenres = await musicStates
            .SelectMany(s => s.Media.Genres, (s, genre) => new { s.PlayCount, Genre = genre })
            .GroupBy(x => x.Genre)
            .Select(g => new GenreStatDto
            {
                Genre = g.Key,
                PlayCount = g.Sum(x => x.PlayCount)
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
