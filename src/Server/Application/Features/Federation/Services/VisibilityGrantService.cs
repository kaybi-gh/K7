using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Federation.Services;

public class VisibilityGrantService(IApplicationDbContext context) : IVisibilityGrantService
{
    public async Task<IReadOnlyList<FederationVisibilityGrantDto>> GetGlobalShareGrantsAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return await context.VisibilityGrants
            .AsNoTracking()
            .Where(g => g.OwnerUserId == ownerUserId && g.PlaylistId == null && g.CollectionId == null)
            .Select(g => new FederationVisibilityGrantDto
            {
                ContentType = g.ContentType,
                TargetUserId = g.TargetUserId,
                TargetPeerServerId = g.TargetPeerServerId,
                TargetOriginUserId = g.TargetOriginUserId
            })
            .ToListAsync(cancellationToken);
    }

    public async Task SetGlobalShareGrantsAsync(
        Guid ownerUserId,
        IReadOnlyList<FederationVisibilityGrantDto> grants,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.VisibilityGrants
            .Where(g => g.OwnerUserId == ownerUserId && g.PlaylistId == null && g.CollectionId == null)
            .ToListAsync(cancellationToken);

        context.VisibilityGrants.RemoveRange(existing);

        foreach (var grant in grants)
        {
            context.VisibilityGrants.Add(new VisibilityGrant
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                ContentType = grant.ContentType,
                TargetUserId = grant.TargetUserId,
                TargetPeerServerId = grant.TargetPeerServerId,
                TargetOriginUserId = grant.TargetOriginUserId
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FederationVisibilityGrantDto>> GetContentGrantsAsync(
        Guid ownerUserId,
        Guid? playlistId,
        Guid? collectionId,
        CancellationToken cancellationToken = default)
    {
        return await context.VisibilityGrants
            .AsNoTracking()
            .Where(g => g.OwnerUserId == ownerUserId
                && g.PlaylistId == playlistId
                && g.CollectionId == collectionId)
            .Select(g => new FederationVisibilityGrantDto
            {
                ContentType = g.ContentType,
                TargetUserId = g.TargetUserId,
                TargetPeerServerId = g.TargetPeerServerId,
                TargetOriginUserId = g.TargetOriginUserId
            })
            .ToListAsync(cancellationToken);
    }

    public async Task SetContentGrantsAsync(
        Guid ownerUserId,
        Guid? playlistId,
        Guid? collectionId,
        FederationContentType? contentType,
        IReadOnlyList<FederationVisibilityGrantDto> grants,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.VisibilityGrants
            .Where(g => g.OwnerUserId == ownerUserId
                && g.PlaylistId == playlistId
                && g.CollectionId == collectionId)
            .ToListAsync(cancellationToken);

        context.VisibilityGrants.RemoveRange(existing);

        foreach (var grant in grants)
        {
            context.VisibilityGrants.Add(new VisibilityGrant
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                ContentType = contentType ?? grant.ContentType,
                PlaylistId = playlistId,
                CollectionId = collectionId,
                TargetUserId = grant.TargetUserId,
                TargetPeerServerId = grant.TargetPeerServerId,
                TargetOriginUserId = grant.TargetOriginUserId
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
