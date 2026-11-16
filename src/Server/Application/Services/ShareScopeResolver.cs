using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Services;

/// <summary>
/// Resolves what a share covers: its root media and everything below it.
/// An episode belongs to its season and series, a track to its album and that album's artist.
/// Nothing above or next to the root is ever in scope.
/// </summary>
public interface IShareScopeResolver
{
    /// <summary>
    /// The media id plus the ids of its parents. Empty when the media does not exist.
    /// A share covers the media when its root is one of these ids.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetSelfAndAncestorIdsAsync(Guid mediaId, CancellationToken cancellationToken = default);

    Task<bool> IsInScopeAsync(Guid rootMediaId, Guid mediaId, CancellationToken cancellationToken = default);
}

public class ShareScopeResolver(IApplicationDbContext context) : IShareScopeResolver
{
    /// <summary>
    /// Media types that are a single playable item. Only these can close a share once consumed.
    /// </summary>
    public static bool IsSinglePlayableItem(MediaType type) =>
        type is MediaType.Movie or MediaType.SerieEpisode or MediaType.MusicTrack;

    public async Task<bool> IsInScopeAsync(Guid rootMediaId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        if (rootMediaId == mediaId)
            return await context.Medias.AsNoTracking().AnyAsync(m => m.Id == mediaId, cancellationToken);

        var ids = await GetSelfAndAncestorIdsAsync(mediaId, cancellationToken);
        return ids.Contains(rootMediaId);
    }

    public async Task<IReadOnlySet<Guid>> GetSelfAndAncestorIdsAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        var type = await context.Medias
            .AsNoTracking()
            .Where(m => m.Id == mediaId)
            .Select(m => (MediaType?)m.Type)
            .FirstOrDefaultAsync(cancellationToken);

        var ids = new HashSet<Guid>();
        if (type is null)
            return ids;

        ids.Add(mediaId);

        switch (type.Value)
        {
            case MediaType.SerieEpisode:
                {
                    var parents = await context.Medias
                        .OfType<SerieEpisode>()
                        .AsNoTracking()
                        .Where(e => e.Id == mediaId)
                        .Select(e => new { e.SeasonId, e.SerieId })
                        .FirstOrDefaultAsync(cancellationToken);
                    if (parents is not null)
                    {
                        ids.Add(parents.SeasonId);
                        ids.Add(parents.SerieId);
                    }

                    break;
                }
            case MediaType.SerieSeason:
                {
                    var serieId = await context.Medias
                        .OfType<SerieSeason>()
                        .AsNoTracking()
                        .Where(s => s.Id == mediaId)
                        .Select(s => (Guid?)s.SerieId)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (serieId is { } id)
                        ids.Add(id);

                    break;
                }
            case MediaType.MusicTrack:
                {
                    // The artist is the album artist: a track only credited on someone else's
                    // album is not part of an artist share.
                    var parents = await context.Medias
                        .OfType<MusicTrack>()
                        .AsNoTracking()
                        .Where(t => t.Id == mediaId)
                        .Select(t => new { t.AlbumId, AlbumArtistId = t.Album.ArtistId })
                        .FirstOrDefaultAsync(cancellationToken);
                    if (parents is not null)
                    {
                        ids.Add(parents.AlbumId);
                        if (parents.AlbumArtistId is { } artistId)
                            ids.Add(artistId);
                    }

                    break;
                }
            case MediaType.MusicAlbum:
                {
                    var artistId = await context.Medias
                        .OfType<MusicAlbum>()
                        .AsNoTracking()
                        .Where(a => a.Id == mediaId)
                        .Select(a => a.ArtistId)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (artistId is { } id)
                        ids.Add(id);

                    break;
                }
        }

        return ids;
    }
}
