using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Features.Medias.Queries.Common;
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
        if (request.Field is not (
            nameof(DynamicPlaylistField.ActorName)
            or nameof(DynamicPlaylistField.ArtistName)
            or nameof(DynamicPlaylistField.Title)
            or nameof(DynamicPlaylistField.AlbumTitle)))
            return [];

        var limit = Math.Clamp(request.Limit, 1, MaxLimit);
        var search = request.SearchText?.Trim();

        var mediaIds = await BrowseMediaScope.GetMediaIdsAsync(
            context,
            request.LibraryIds,
            request.LibraryGroupIds,
            request.MediaTypes,
            currentUser.Id,
            unwatchedOnly: null,
            cancellationToken);

        return request.Field switch
        {
            nameof(DynamicPlaylistField.ActorName) => await SearchActorNamesAsync(mediaIds, search, limit, cancellationToken),
            nameof(DynamicPlaylistField.ArtistName) => await SearchArtistNamesAsync(mediaIds, search, limit, cancellationToken),
            nameof(DynamicPlaylistField.Title) => await SearchTitlesAsync(mediaIds, search, limit, cancellationToken),
            nameof(DynamicPlaylistField.AlbumTitle) => await SearchAlbumTitlesAsync(mediaIds, search, limit, cancellationToken),
            _ => []
        };
    }

    private async Task<IReadOnlyList<string>> SearchActorNamesAsync(
        IQueryable<Guid> mediaIds,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = context.PersonRoles.AsNoTracking()
            .Where(r => mediaIds.Contains(r.MediaId)
                && (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor)
                && r.Person.Name != null);

        if (!string.IsNullOrEmpty(search))
        {
            var term = EfLikeQueryExtensions.ToLowerSearchTerm(search);
            query = query.Where(r => r.Person.Name!.ToLower().Contains(term));
        }

        return await query
            .Select(r => r.Person.Name)
            .Distinct()
            .OrderBy(name => name)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<string>> SearchArtistNamesAsync(
        IQueryable<Guid> mediaIds,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        var term = string.IsNullOrEmpty(search) ? null : EfLikeQueryExtensions.ToLowerSearchTerm(search);

        var artistTitles = context.Medias.OfType<MusicArtist>().AsNoTracking()
            .Where(a => mediaIds.Contains(a.Id) && a.Title != null
                && (term == null || a.Title.ToLower().Contains(term)))
            .Select(a => a.Title!);

        var albumArtists = context.Medias.OfType<MusicAlbum>().AsNoTracking()
            .Where(a => mediaIds.Contains(a.Id) && a.Artist != null && a.Artist.Title != null
                && (term == null || a.Artist.Title.ToLower().Contains(term)))
            .Select(a => a.Artist!.Title!);

        var trackArtists = context.Medias.OfType<MusicTrack>().AsNoTracking()
            .Where(t => mediaIds.Contains(t.Id)
                && ((t.Artist != null && t.Artist.Title != null && (term == null || t.Artist.Title.ToLower().Contains(term)))
                    || (t.Artist == null && t.Album.Artist != null && t.Album.Artist.Title != null
                        && (term == null || t.Album.Artist.Title.ToLower().Contains(term)))))
            .Select(t => t.Artist != null ? t.Artist.Title! : t.Album.Artist!.Title!);

        return await artistTitles
            .Concat(albumArtists)
            .Concat(trackArtists)
            .Distinct()
            .OrderBy(name => name)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<string>> SearchTitlesAsync(
        IQueryable<Guid> mediaIds,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = context.Medias.AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id) && m.Title != null);

        if (!string.IsNullOrEmpty(search))
        {
            var term = EfLikeQueryExtensions.ToLowerSearchTerm(search);
            query = query.Where(m => m.Title!.ToLower().Contains(term));
        }

        return await query
            .Select(m => m.Title!)
            .Distinct()
            .OrderBy(title => title)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<string>> SearchAlbumTitlesAsync(
        IQueryable<Guid> mediaIds,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        var term = string.IsNullOrEmpty(search) ? null : EfLikeQueryExtensions.ToLowerSearchTerm(search);

        var albumTitles = context.Medias.OfType<MusicAlbum>().AsNoTracking()
            .Where(a => mediaIds.Contains(a.Id) && a.Title != null
                && (term == null || a.Title.ToLower().Contains(term)))
            .Select(a => a.Title!);

        var trackAlbumTitles = context.Medias.OfType<MusicTrack>().AsNoTracking()
            .Where(t => mediaIds.Contains(t.Id) && t.Album.Title != null
                && (term == null || t.Album.Title.ToLower().Contains(term)))
            .Select(t => t.Album.Title!);

        return await albumTitles
            .Concat(trackAlbumTitles)
            .Distinct()
            .OrderBy(title => title)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
