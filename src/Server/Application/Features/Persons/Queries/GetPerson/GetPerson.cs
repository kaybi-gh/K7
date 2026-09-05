using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;

namespace K7.Server.Application.Features.Persons.Queries.GetPerson;

public record GetPersonQuery(Guid Id) : IRequest<Person>;

public class GetPersonQueryHandler(IApplicationDbContext context, IUser currentUser, MediaAccessFilter mediaAccessFilter)
    : IRequestHandler<GetPersonQuery, Person>
{
    public async Task<Person> Handle(GetPersonQuery request, CancellationToken cancellationToken)
    {
        var entity = await context.Persons
        .AsNoTracking()
        .AsSplitQuery()
        .Include(x => x.ExternalIds)
        .Include(x => x.PortraitPicture)
        .Include(x => x.Roles)
            .ThenInclude(x => x.PortraitPicture)
        .Include(x => x.Roles)
            .ThenInclude(x => x.Media)
                .ThenInclude(x => x.Pictures)
                    .ThenInclude(p => p.Variants)
        .Include(x => x.Roles)
            .ThenInclude(x => x.Media)
                .ThenInclude(x => x.IndexedFiles)
                    .ThenInclude(f => f.FileMetadata)
        .Include(x => x.Roles)
            .ThenInclude(x => x.Media)
                .ThenInclude(x => x.RemoteIndexedFiles)
        .Include(x => x.Roles)
            .ThenInclude(x => x.Media)
                .ThenInclude(x => x.ExternalIds)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as MusicTrack)!.Album)
                .ThenInclude(a => a.Pictures)
                    .ThenInclude(p => p.Variants)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as MusicAlbum)!.Tracks)
                .ThenInclude(t => t.IndexedFiles)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as MusicAlbum)!.Tracks)
                .ThenInclude(t => t.RemoteIndexedFiles)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as MusicArtist)!.Albums)
                .ThenInclude(a => a.Pictures)
                    .ThenInclude(p => p.Variants)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as MusicArtist)!.Albums)
                .ThenInclude(a => a.Tracks)
                    .ThenInclude(t => t.IndexedFiles)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as MusicArtist)!.Albums)
                .ThenInclude(a => a.Tracks)
                    .ThenInclude(t => t.RemoteIndexedFiles)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as MusicArtist)!.Albums)
                .ThenInclude(a => a.RemoteIndexedFiles)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as MusicArtist)!.ArtistCredits)
                .ThenInclude(c => c.Media)
                    .ThenInclude(m => (m as MusicTrack)!.IndexedFiles)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as MusicArtist)!.ArtistCredits)
                .ThenInclude(c => c.Media)
                    .ThenInclude(m => (m as MusicTrack)!.RemoteIndexedFiles)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as MusicArtist)!.ArtistCredits)
                .ThenInclude(c => c.Media)
                    .ThenInclude(m => (m as MusicTrack)!.Album)
                        .ThenInclude(a => a.Pictures)
                            .ThenInclude(p => p.Variants)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as SerieEpisode)!.Serie)
                .ThenInclude(s => s.Pictures)
                    .ThenInclude(p => p.Variants)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as SerieEpisode)!.Serie)
                .ThenInclude(s => s.Seasons)
                    .ThenInclude(se => se.Episodes)
                        .ThenInclude(e => e.IndexedFiles)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as SerieEpisode)!.Serie)
                .ThenInclude(s => s.Seasons)
                    .ThenInclude(se => se.Episodes)
                        .ThenInclude(e => e.RemoteIndexedFiles)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as SerieEpisode)!.Serie)
                .ThenInclude(s => s.ExternalIds)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as SerieEpisode)!.Season)
                .ThenInclude(s => s.Pictures)
                    .ThenInclude(p => p.Variants)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as Serie)!.Seasons)
                .ThenInclude(s => s.Episodes)
                    .ThenInclude(e => e.IndexedFiles)
        .Include(x => x.Roles)
            .ThenInclude(x => (x.Media as Serie)!.Seasons)
                .ThenInclude(s => s.Episodes)
                    .ThenInclude(e => e.RemoteIndexedFiles)
        .Where(x => x.Id == request.Id)
        .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        HashSet<Guid>? excludedLibraryIds = null;
        if (currentUser.Id is { } userId)
        {
            excludedLibraryIds = await context.UserLibraryExclusions
                .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.LibraryId)
                .ToHashSetAsync(cancellationToken);
        }

        entity.Roles = PersonRoleAvailabilityHelper.FilterPlayableRoles(entity.Roles, excludedLibraryIds);

        if (currentUser.Id is { } restrictionUserId)
            entity.Roles = await FilterRestrictedRolesAsync(entity.Roles, restrictionUserId, cancellationToken);

        return entity;
    }

    private async Task<List<BasePersonRole>> FilterRestrictedRolesAsync(
        IEnumerable<BasePersonRole> roles,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);
        var restrictionProfile = await mediaAccessFilter.GetRestrictionProfileAsync(
            userId, sharedProfileId, cancellationToken);
        if (restrictionProfile is null)
            return roles.ToList();

        var roleMediaIds = roles.Select(r => r.MediaId).Distinct().ToList();
        var allowedMediaIds = await ContentRestrictionEvaluator.ApplyRestriction(
                context.Medias.AsNoTracking().Where(m => roleMediaIds.Contains(m.Id)),
                restrictionProfile)
            .Select(m => m.Id)
            .ToHashSetAsync(cancellationToken);

        return roles.Where(r => allowedMediaIds.Contains(r.MediaId)).ToList();
    }
}
