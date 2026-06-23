using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Medias.Queries.GetSimilarMusicArtists;

[Authorize(Roles = $"{Roles.Guest},{Roles.User},{Roles.Administrator}")]
public record GetSimilarMusicArtistsQuery : IRequest<IReadOnlyList<LiteMusicArtistDto>>
{
    public required Guid ArtistId { get; init; }
    public int Count { get; init; } = 12;
}

public class GetSimilarMusicArtistsQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IMediaAccessGuard accessGuard,
    IMusicIntelligenceService musicIntelligenceService)
    : IRequestHandler<GetSimilarMusicArtistsQuery, IReadOnlyList<LiteMusicArtistDto>>
{
    public async Task<IReadOnlyList<LiteMusicArtistDto>> Handle(
        GetSimilarMusicArtistsQuery request,
        CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(request.ArtistId, cancellationToken);

        if (!await musicIntelligenceService.IsAvailableAsync(cancellationToken))
            return [];

        var artist = await context.Medias
            .AsNoTracking()
            .OfType<MusicArtist>()
            .FirstOrDefaultAsync(a => a.Id == request.ArtistId, cancellationToken);

        if (artist is null)
            return [];

        var candidateCount = Math.Clamp(request.Count * 3, request.Count, 36);
        var matches = await musicIntelligenceService.GetSimilarArtistsAsync(
            request.ArtistId,
            artist.Title,
            candidateCount,
            cancellationToken);

        if (matches.Count == 0)
            return [];

        var parsedIds = matches
            .Select(m => Guid.TryParse(m.ArtistId, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue && id != request.ArtistId)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var artistsById = await LoadArtistsByIdsAsync(parsedIds, currentUser.Id, cancellationToken);

        var unresolvedNames = matches
            .Where(m => !Guid.TryParse(m.ArtistId, out var id) || !artistsById.ContainsKey(id))
            .Select(m => m.Artist)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var artistsByName = unresolvedNames.Count == 0
            ? new Dictionary<string, MusicArtist>(StringComparer.OrdinalIgnoreCase)
            : await LoadArtistsByNamesAsync(unresolvedNames, request.ArtistId, currentUser.Id, cancellationToken);

        var result = new List<LiteMusicArtistDto>();
        var seen = new HashSet<Guid>();

        foreach (var match in matches)
        {
            MusicArtist? resolved = null;

            if (Guid.TryParse(match.ArtistId, out var id) && artistsById.TryGetValue(id, out var byId))
                resolved = byId;
            else if (!string.IsNullOrWhiteSpace(match.Artist) && artistsByName.TryGetValue(match.Artist, out var byName))
                resolved = byName;

            if (resolved is null || !seen.Add(resolved.Id))
                continue;

            result.Add((LiteMusicArtistDto)resolved.ToLiteMediaDto());
            if (result.Count >= request.Count)
                break;
        }

        return result;
    }

    private async Task<Dictionary<Guid, MusicArtist>> LoadArtistsByIdsAsync(
        IReadOnlyList<Guid> artistIds,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if (artistIds.Count == 0)
            return [];

        var query = context.Medias
            .AsNoTracking()
            .OfType<MusicArtist>()
            .Include(a => a.Pictures)
                .ThenInclude(p => p.Variants)
            .Include(a => a.Ratings)
            .Where(a => artistIds.Contains(a.Id))
            .AsSplitQuery();

        if (userId.HasValue)
            query = query.Include(a => a.UserMediaStates.Where(s => s.UserId == userId.Value));

        var artists = await query.ToListAsync(cancellationToken);
        return artists.ToDictionary(a => a.Id);
    }

    private async Task<Dictionary<string, MusicArtist>> LoadArtistsByNamesAsync(
        IReadOnlyList<string> names,
        Guid excludeArtistId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var lowered = names
            .Select(n => n.ToLowerInvariant())
            .ToHashSet();

        var query = context.Medias
            .AsNoTracking()
            .OfType<MusicArtist>()
            .Include(a => a.Pictures)
                .ThenInclude(p => p.Variants)
            .Include(a => a.Ratings)
            .Where(a => a.Id != excludeArtistId && a.Title != null)
            .AsSplitQuery();

        if (userId.HasValue)
            query = query.Include(a => a.UserMediaStates.Where(s => s.UserId == userId.Value));

        var artists = await query.ToListAsync(cancellationToken);

        return artists
            .Where(a => lowered.Contains(a.Title!.ToLowerInvariant()))
            .GroupBy(a => a.Title!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }
}
