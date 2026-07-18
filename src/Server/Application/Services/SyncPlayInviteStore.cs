using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Application.Services;

/// <summary>
/// DB-backed persistence for SyncPlay invite tokens so links survive a server restart.
/// Scoped (uses <see cref="IApplicationDbContext"/>) - consumed by the singleton
/// <see cref="SyncPlayCoordinator"/> via <see cref="IServiceScopeFactory"/>.
/// </summary>
public interface ISyncPlayInviteStore
{
    Task AddAsync(string token, Guid groupId, string createdByUserId, CancellationToken cancellationToken = default);
    Task<Guid?> ResolveGroupIdAsync(string token, CancellationToken cancellationToken = default);
    Task PurgeOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default);
}

public class SyncPlayInviteStore(IApplicationDbContext context) : ISyncPlayInviteStore
{
    public async Task AddAsync(string token, Guid groupId, string createdByUserId, CancellationToken cancellationToken = default)
    {
        context.SyncPlayInvites.Add(new SyncPlayInvite
        {
            Id = Guid.NewGuid(),
            Token = token,
            GroupId = groupId,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid?> ResolveGroupIdAsync(string token, CancellationToken cancellationToken = default)
    {
        var invite = await context.SyncPlayInvites
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Token == token, cancellationToken);

        return invite?.GroupId;
    }

    public async Task PurgeOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default)
    {
        var stale = await context.SyncPlayInvites
            .Where(x => x.CreatedAt < cutoffUtc)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
            return;

        context.SyncPlayInvites.RemoveRange(stale);
        await context.SaveChangesAsync(cancellationToken);
    }
}
