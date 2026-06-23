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
        if (request.Field is not (nameof(SmartPlaylistField.ActorName) or nameof(SmartPlaylistField.ArtistName)))
            return [];

        var limit = Math.Clamp(request.Limit, 1, MaxLimit);
        var search = request.SearchText?.Trim();
        if (string.IsNullOrEmpty(search))
            return [];

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
            nameof(SmartPlaylistField.ActorName) => await SearchActorNamesAsync(mediaIds, search, limit, cancellationToken),
            nameof(SmartPlaylistField.ArtistName) => await SearchArtistNamesAsync(mediaIds, search, limit, cancellationToken),
            _ => []
        };
    }

    private async Task<IReadOnlyList<string>> SearchActorNamesAsync(
        IQueryable<Guid> mediaIds,
        string search,
        int limit,
        CancellationToken cancellationToken)
    {
        var pattern = EfLikeQueryExtensions.ToContainsPattern(search);

        return await context.PersonRoles.AsNoTracking()
            .Where(r => mediaIds.Contains(r.MediaId)
                && (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor)
                && EF.Functions.Like(r.Person.Name.ToLower(), pattern))
            .Select(r => r.Person.Name)
            .Distinct()
            .OrderBy(name => name)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<string>> SearchArtistNamesAsync(
        IQueryable<Guid> mediaIds,
        string search,
        int limit,
        CancellationToken cancellationToken)
    {
        var pattern = EfLikeQueryExtensions.ToContainsPattern(search);

        var artistTitles = context.Medias.OfType<MusicArtist>().AsNoTracking()
            .Where(a => mediaIds.Contains(a.Id) && a.Title != null && EF.Functions.Like(a.Title.ToLower(), pattern))
            .Select(a => a.Title!);

        var albumArtists = context.Medias.OfType<MusicAlbum>().AsNoTracking()
            .Where(a => mediaIds.Contains(a.Id) && a.Artist != null && a.Artist.Title != null
                && EF.Functions.Like(a.Artist.Title.ToLower(), pattern))
            .Select(a => a.Artist!.Title!);

        var trackArtists = context.Medias.OfType<MusicTrack>().AsNoTracking()
            .Where(t => mediaIds.Contains(t.Id)
                && ((t.Artist != null && t.Artist.Title != null && EF.Functions.Like(t.Artist.Title.ToLower(), pattern))
                    || (t.Artist == null && t.Album.Artist != null && t.Album.Artist.Title != null
                        && EF.Functions.Like(t.Album.Artist.Title.ToLower(), pattern))))
            .Select(t => t.Artist != null ? t.Artist.Title! : t.Album.Artist!.Title!);

        return await artistTitles
            .Concat(albumArtists)
            .Concat(trackArtists)
            .Distinct()
            .OrderBy(name => name)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
