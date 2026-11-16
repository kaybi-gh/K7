using K7.Server.Application.Common;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Settings;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Shares;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.MediaShares.Services;

/// <summary>
/// The user managing shares in the current request.
/// </summary>
public sealed record ShareActor(Guid UserId, bool IsAdmin);

/// <summary>
/// Rules shared by every share handler: is the feature on, who may share,
/// and which shares a user may manage.
/// </summary>
public interface IMediaShareManager
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws when public shares are turned off on this server.
    /// </summary>
    Task EnsureEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The signed-in user. Administrators manage every share, others only their own.
    /// </summary>
    Task<ShareActor> RequireActorAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Same as <see cref="RequireActorAsync"/>, and the user must hold the share capability.
    /// </summary>
    Task<ShareActor> RequireSharingActorAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the user is active and still allowed to share. Shares of a user that
    /// lost this are suspended.
    /// </summary>
    Task<bool> CanUserShareAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a tracked share the actor may manage. Not found otherwise.
    /// </summary>
    Task<MediaShare> GetManagedShareAsync(Guid shareId, ShareActor actor, CancellationToken cancellationToken = default);
}

public class MediaShareManager(
    IApplicationDbContext context,
    IUser user,
    IIdentityService identityService,
    IServerSettingsService serverSettingsService) : IMediaShareManager
{
    private readonly Dictionary<Guid, bool> _canShareCache = [];

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        var flags = await serverSettingsService.GetFeatureFlagsAsync(cancellationToken);
        return flags.PublicSharesEnabled;
    }

    public async Task EnsureEnabledAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsEnabledAsync(cancellationToken))
            throw new UnprocessableEntityException("Public shares are disabled on this server.");
    }

    public async Task<ShareActor> RequireActorAsync(CancellationToken cancellationToken = default)
    {
        var userId = await user.GetIdAsync(cancellationToken);
        if (userId is not { } id || string.IsNullOrEmpty(user.IdentityId))
            throw new UnauthorizedAccessException();

        var isAdmin = user.IsInRole(Roles.Administrator)
            || await identityService.IsInRoleAsync(user.IdentityId, Roles.Administrator);

        return new ShareActor(id, isAdmin);
    }

    public async Task<ShareActor> RequireSharingActorAsync(CancellationToken cancellationToken = default)
    {
        var actor = await RequireActorAsync(cancellationToken);
        if (!await CanUserShareAsync(actor.UserId, cancellationToken))
            throw new ForbiddenAccessException();

        return actor;
    }

    public async Task<bool> CanUserShareAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_canShareCache.TryGetValue(userId, out var cached))
            return cached;

        var account = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.DeletedAt })
            .FirstOrDefaultAsync(cancellationToken);

        var allowed = account is { IsActive: true, DeletedAt: null }
            && await UserCapabilityEvaluator.HasAsync(
                context,
                identityService,
                userId,
                Capability.CanShareMedia,
                cancellationToken);

        _canShareCache[userId] = allowed;
        return allowed;
    }

    public async Task<MediaShare> GetManagedShareAsync(Guid shareId, ShareActor actor, CancellationToken cancellationToken = default)
    {
        var share = await context.MediaShares
            .FirstOrDefaultAsync(s => s.Id == shareId, cancellationToken);

        if (share is null || (!actor.IsAdmin && share.CreatedByUserId != actor.UserId))
            throw new NotFoundException(shareId.ToString(), nameof(MediaShare));

        return share;
    }

    /// <summary>
    /// Status shown to owners and visitors: a share that would be active is suspended
    /// while its creator is not allowed to share.
    /// </summary>
    public static MediaShareStatus ResolveStatus(MediaShare share, bool creatorCanShare, DateTimeOffset utcNow)
    {
        var status = share.GetStatus(utcNow);
        return status == MediaShareStatus.Active && !creatorCanShare
            ? MediaShareStatus.Suspended
            : status;
    }
}
