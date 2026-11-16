using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Shares;

namespace K7.Server.Application.Services;

/// <summary>
/// A share opened by the current visitor that covers the requested media.
/// </summary>
public sealed record ShareGrantMatch(
    Guid ShareId,
    Guid SessionId,
    Guid RootMediaId,
    Guid CreatedByUserId,
    bool AllowPlayback,
    bool AllowDownload);

/// <summary>
/// Maps the shares the current visitor opened to the media they cover.
/// </summary>
public interface IShareVisitorAccess
{
    bool IsShareVisitor { get; }

    /// <summary>
    /// The visitor's grant for this share. Throws not found when the visitor did not open it.
    /// </summary>
    ShareVisitorGrant RequireGrant(Guid shareId);

    Task<IReadOnlyList<ShareGrantMatch>> GetMatchingSharesAsync(Guid mediaId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShareGrantMatch>> GetMatchingSharesByIndexedFileAsync(Guid indexedFileId, CancellationToken cancellationToken = default);
}

public class ShareVisitorAccess(
    IApplicationDbContext context,
    IShareVisitorContext visitorContext,
    IShareScopeResolver scopeResolver) : IShareVisitorAccess
{
    public bool IsShareVisitor => visitorContext.IsShareVisitor;

    public ShareVisitorGrant RequireGrant(Guid shareId)
    {
        var grant = visitorContext.IsShareVisitor
            ? visitorContext.Grants.FirstOrDefault(g => g.ShareId == shareId)
            : null;

        return grant ?? throw new NotFoundException(shareId.ToString(), nameof(MediaShare));
    }

    public async Task<IReadOnlyList<ShareGrantMatch>> GetMatchingSharesAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        if (!visitorContext.IsShareVisitor || visitorContext.Grants.Count == 0)
            return [];

        var shareIds = visitorContext.Grants.Select(g => g.ShareId).ToList();
        var shares = await context.MediaShares
            .AsNoTracking()
            .Where(s => shareIds.Contains(s.Id) && s.RevokedAt == null)
            .Select(s => new
            {
                s.Id,
                s.RootMediaId,
                s.CreatedByUserId,
                s.AllowPlayback,
                s.AllowDownload,
                s.ExpiresAt
            })
            .ToListAsync(cancellationToken);

        if (shares.Count == 0)
            return [];

        var scopeIds = await scopeResolver.GetSelfAndAncestorIdsAsync(mediaId, cancellationToken);
        if (scopeIds.Count == 0)
            return [];

        var now = DateTimeOffset.UtcNow;
        return shares
            .Where(s => scopeIds.Contains(s.RootMediaId))
            .Where(s => s.ExpiresAt is null || s.ExpiresAt > now)
            .Select(s => new ShareGrantMatch(
                s.Id,
                visitorContext.Grants.First(g => g.ShareId == s.Id).SessionId,
                s.RootMediaId,
                s.CreatedByUserId,
                s.AllowPlayback,
                s.AllowDownload))
            .ToList();
    }

    public async Task<IReadOnlyList<ShareGrantMatch>> GetMatchingSharesByIndexedFileAsync(Guid indexedFileId, CancellationToken cancellationToken = default)
    {
        if (!visitorContext.IsShareVisitor || visitorContext.Grants.Count == 0)
            return [];

        var mediaId = await context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.Id == indexedFileId)
            .Select(f => f.MediaId)
            .FirstOrDefaultAsync(cancellationToken);

        // A file that is not attached to a media is in no share.
        return mediaId is { } id
            ? await GetMatchingSharesAsync(id, cancellationToken)
            : [];
    }
}
