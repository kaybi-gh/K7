using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Medias.Queries.GetMedia;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;

namespace K7.Server.Application.Features.MediaShares.Queries.GetSharedMedia;

/// <summary>
/// A media page as a share visitor sees it: the media must be inside the share, and the
/// result carries nothing personal (no user state, no ratings by users, no disk location,
/// no library id, no files hosted on other servers).
/// </summary>
public record GetSharedMediaQuery(Guid ShareId, Guid MediaId) : IRequest<BaseMedia>;

public class GetSharedMediaQueryHandler(
    IApplicationDbContext context,
    IShareVisitorAccess shareVisitorAccess,
    IShareScopeResolver scopeResolver,
    IMediaAccessGuard accessGuard,
    MediaAccessFilter mediaAccessFilter,
    ISender sender)
    : IRequestHandler<GetSharedMediaQuery, BaseMedia>
{
    public async Task<BaseMedia> Handle(GetSharedMediaQuery request, CancellationToken cancellationToken)
    {
        shareVisitorAccess.RequireGrant(request.ShareId);

        var share = await context.MediaShares
            .AsNoTracking()
            .Where(s => s.Id == request.ShareId)
            .Select(s => new { s.RootMediaId, s.CreatedByUserId })
            .FirstOrDefaultAsync(cancellationToken);

        // The scope is checked against this share only, even when the visitor opened others.
        if (share is null || !await scopeResolver.IsInScopeAsync(share.RootMediaId, request.MediaId, cancellationToken))
            throw new NotFoundException(request.MediaId.ToString(), nameof(BaseMedia));

        // The regular query applies the access guard, which also checks the share creator's rights.
        var result = await sender.Send(new GetMediaQuery(request.MediaId), cancellationToken);
        var media = result.Media;

        // Children the creator cannot see are not shared either: hidden media and libraries,
        // then restriction profile and age gate.
        await MediaGraphExclusionFilter.ApplyAsync(context, media, share.CreatedByUserId, cancellationToken);
        await PruneRestrictedChildrenAsync(media, share.CreatedByUserId, cancellationToken);

        foreach (var node in Flatten(media))
            Sanitize(node);

        return media;
    }

    /// <summary>
    /// Only runs for creators with a restriction profile or an age gate, which is rare:
    /// it checks every child one by one.
    /// </summary>
    private async Task PruneRestrictedChildrenAsync(BaseMedia media, Guid creatorUserId, CancellationToken cancellationToken)
    {
        var gates = await mediaAccessFilter.GetContentGatesAsync(creatorUserId, sharedProfileId: null, cancellationToken);
        if (gates.RestrictionProfile is null && gates.AgeGate is null)
            return;

        switch (media)
        {
            case Serie serie:
                serie.Seasons = await KeepAccessibleAsync(serie.Seasons, creatorUserId, cancellationToken);
                foreach (var season in serie.Seasons)
                    season.Episodes = await KeepAccessibleAsync(season.Episodes, creatorUserId, cancellationToken);

                break;
            case SerieSeason season:
                season.Episodes = await KeepAccessibleAsync(season.Episodes, creatorUserId, cancellationToken);
                break;
            case MusicArtist artist:
                artist.Albums = await KeepAccessibleAsync(artist.Albums, creatorUserId, cancellationToken);
                foreach (var album in artist.Albums)
                    album.Tracks = await KeepAccessibleAsync(album.Tracks, creatorUserId, cancellationToken);

                break;
            case MusicAlbum album:
                album.Tracks = await KeepAccessibleAsync(album.Tracks, creatorUserId, cancellationToken);
                break;
        }
    }

    private async Task<IList<T>> KeepAccessibleAsync<T>(IList<T> children, Guid creatorUserId, CancellationToken cancellationToken)
        where T : BaseMedia
    {
        var kept = new List<T>(children.Count);
        foreach (var child in children)
        {
            if (await accessGuard.CanAccessAsync(child.Id, creatorUserId, cancellationToken))
                kept.Add(child);
        }

        return kept;
    }

    private static IEnumerable<BaseMedia> Flatten(BaseMedia media)
    {
        yield return media;

        switch (media)
        {
            case Serie serie:
                foreach (var season in serie.Seasons)
                {
                    yield return season;
                    foreach (var episode in season.Episodes)
                        yield return episode;
                }

                break;
            case SerieSeason season:
                foreach (var episode in season.Episodes)
                    yield return episode;

                break;
            case MusicArtist artist:
                foreach (var album in artist.Albums)
                {
                    yield return album;
                    foreach (var track in album.Tracks)
                        yield return track;
                }

                break;
            case MusicAlbum album:
                foreach (var track in album.Tracks)
                    yield return track;

                break;
        }
    }

    private static void Sanitize(BaseMedia media)
    {
        media.RemoteIndexedFiles = [];
        media.UserMediaStates = [];
        media.Ratings = media.Ratings.Where(r => r is not UserRating).ToList();

        foreach (var file in media.IndexedFiles)
            HideServerDetails(file);
    }

    // Visitors get the file name for downloads, never where it lives on the server.
    private static void HideServerDetails(IndexedFile file)
    {
        file.Path = Path.GetFileName(file.Path);
        file.ParentDirectory = null;
        file.Identification = null;
        file.LibraryId = Guid.Empty;
        file.Hash = 0;
    }
}
