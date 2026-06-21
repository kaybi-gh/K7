using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Features.Medias.Queries.Common;
using K7.Server.Application.Features.Medias.Queries.GetMedias;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Medias.Queries.GetMediaBrowseFilterSuggestions;

public record GetMediaBrowseFilterSuggestionsQuery : IRequest<IReadOnlyList<string>>
{
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? LibraryGroupIds { get; init; }
    public EnumHashSetQueryParam<MediaType>? MediaTypes { get; init; }
    public required string Field { get; init; }
    public string? SearchText { get; init; }
    public int Limit { get; init; } = 20;
}

public class GetMediaBrowseFilterSuggestionsQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetMediaBrowseFilterSuggestionsQuery, IReadOnlyList<string>>
{
    private const int MaxLimit = 50;

    public async Task<IReadOnlyList<string>> Handle(
        GetMediaBrowseFilterSuggestionsQuery request,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, 1, MaxLimit);
        var search = request.SearchText?.Trim();
        if (string.IsNullOrEmpty(search))
            return [];

        var userId = currentUser.Id;

        var libraryIds = await LibraryGroupFilterHelper.ResolveLibraryIdsAsync(
            context, request.LibraryIds, request.LibraryGroupIds, cancellationToken);
        request = request with { LibraryIds = libraryIds, LibraryGroupIds = null };

        var mediasQuery = context.Medias.AsNoTracking().AsQueryable();
        mediasQuery = ApplyMediaFilters(request, mediasQuery, userId);

        if (userId.HasValue)
        {
            var excludedLibraryIds = context.UserLibraryExclusions
                .Where(e => e.UserId == userId.Value && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.LibraryId);

            mediasQuery = mediasQuery.Where(x =>
                x is MusicAlbum
                    ? x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                        || ((MusicAlbum)x).Tracks.Any(t => t.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                            || t.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId)))
                    : x is MusicArtist
                        ? ((MusicArtist)x).Albums.Any(a => a.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                            || a.Tracks.Any(t => t.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                                || t.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))))
                    : x is MusicTrack
                        ? x.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                            || x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                            || ((MusicTrack)x).Album.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                            || ((MusicTrack)x).Album.Tracks.Any(t => t.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId)))
                    : x is Serie
                        ? x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                            || ((Serie)x).Seasons.Any(s => s.Episodes.Any(e => e.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                                || e.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))))
                        : x is SerieSeason
                            ? ((SerieSeason)x).Episodes.Any(e => e.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                                || e.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId)))
                            : x.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                                || x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId)));
        }

        var mediaIds = mediasQuery.Select(m => m.Id);

        return request.Field switch
        {
            nameof(SmartPlaylistField.ActorName) => await SearchActorNamesAsync(mediaIds, search, limit, cancellationToken),
            "Studio" => await SearchStudiosAsync(mediasQuery, search, limit, cancellationToken),
            "Network" => await SearchNetworksAsync(mediasQuery, search, limit, cancellationToken),
            _ => []
        };
    }

    private async Task<IReadOnlyList<string>> SearchActorNamesAsync(
        IQueryable<Guid> mediaIds,
        string search,
        int limit,
        CancellationToken cancellationToken) =>
        await context.PersonRoles.AsNoTracking()
            .Where(r => mediaIds.Contains(r.MediaId)
                && (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor)
                && EF.Functions.Like(r.Person.Name, $"%{search}%"))
            .Select(r => r.Person.Name)
            .Distinct()
            .OrderBy(name => name)
            .Take(limit)
            .ToListAsync(cancellationToken);

    private static async Task<IReadOnlyList<string>> SearchStudiosAsync(
        IQueryable<BaseMedia> mediasQuery,
        string search,
        int limit,
        CancellationToken cancellationToken)
    {
        var movieStudios = await mediasQuery
            .OfType<Movie>()
            .SelectMany(m => m.Studios)
            .Where(s => EF.Functions.Like(s, $"%{search}%"))
            .ToListAsync(cancellationToken);

        var serieStudios = await mediasQuery
            .OfType<Serie>()
            .SelectMany(s => s.Studios)
            .Where(s => EF.Functions.Like(s, $"%{search}%"))
            .ToListAsync(cancellationToken);

        return movieStudios
            .Concat(serieStudios)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private static async Task<IReadOnlyList<string>> SearchNetworksAsync(
        IQueryable<BaseMedia> mediasQuery,
        string search,
        int limit,
        CancellationToken cancellationToken) =>
        await mediasQuery
            .OfType<Serie>()
            .Where(s => s.Network != null && s.Network != "" && EF.Functions.Like(s.Network, $"%{search}%"))
            .Select(s => s.Network!)
            .Distinct()
            .OrderBy(n => n)
            .Take(limit)
            .ToListAsync(cancellationToken);

    private static IQueryable<BaseMedia> ApplyMediaFilters(
        GetMediaBrowseFilterSuggestionsQuery request,
        IQueryable<BaseMedia> query,
        Guid? userId)
    {
        var paginationRequest = new GetMediasWithPaginationQuery
        {
            LibraryIds = request.LibraryIds,
            MediaTypes = request.MediaTypes,
            PageNumber = 1,
            PageSize = 1
        };

        return GetMediasQueryHandler.ApplyFilters(paginationRequest, query, userId);
    }
}
