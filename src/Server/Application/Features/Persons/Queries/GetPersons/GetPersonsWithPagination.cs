using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Medias.Queries.GetPersons;

public record GetPersonsWithPaginationQuery : IRequest<PaginatedList<Person>>
{
    public Guid[]? Ids { get; init; }
    public Guid[]? MediaIds { get; init; }
    public EnumHashSetQueryParam<PersonRoleType>? RoleTypes { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}

public class GetPersonsQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetPersonsWithPaginationQuery, PaginatedList<Person>>
{
    public async Task<PaginatedList<Person>> Handle(GetPersonsWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var query = context.Persons
            .Include(x => x.PortraitPicture)
            .Include(x => x.ExternalIds)
            .Include(x => x.Roles)
                .ThenInclude(x => x.Media)
            .AsQueryable();

        query = ApplyFilters(request, query);

        if (currentUser.Id is { } userId)
        {
            var excludedLibraryIds = context.UserLibraryExclusions
                .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.LibraryId);

            var roleFilter = request.RoleTypes;

            query = query.Where(p => p.Roles.Any(r =>
                (roleFilter == null || roleFilter.Count == 0 || roleFilter.Contains(r.Type)) &&
                (
                    r.Media!.RemoteIndexedFiles.Any()
                    || (r.Media is MusicAlbum
                        ? ((MusicAlbum)r.Media).Tracks.Any(t =>
                            t.RemoteIndexedFiles.Any()
                            || t.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId)))
                        : r.Media is Serie
                            ? ((Serie)r.Media).Seasons.Any(s => s.Episodes.Any(e =>
                                e.RemoteIndexedFiles.Any()
                                || e.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))))
                            : r.Media is SerieEpisode
                                ? ((SerieEpisode)r.Media).RemoteIndexedFiles.Any()
                                    || ((SerieEpisode)r.Media).IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                                    || ((SerieEpisode)r.Media).Serie!.Seasons.Any(s => s.Episodes.Any(e =>
                                        e.RemoteIndexedFiles.Any()
                                        || e.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))))
                                : r.Media is MusicTrack
                                    ? ((MusicTrack)r.Media).RemoteIndexedFiles.Any()
                                        || ((MusicTrack)r.Media).IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                                    : r.Media.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId)))
                )));
        }

        return await query.AsSplitQuery().PaginatedListAsync(request.PageNumber, request.PageSize);
    }

    private static IQueryable<Person> ApplyFilters(GetPersonsWithPaginationQuery request, IQueryable<Person> query)
    {
        if (request.Ids?.Length > 0)
        {
            query = query.Where(x => request.Ids.Contains(x.Id));
        }

        if (request.MediaIds?.Length > 0)
        {
            query = query.Where(x => x.Roles.Any(x => request.MediaIds.Contains(x.MediaId)));
        }

        if (request.RoleTypes?.Count > 0)
        {
            query = query.Where(x => x.Roles.Any(r => request.RoleTypes.Contains(r.Type)));
        }

        return query;
    }
}
