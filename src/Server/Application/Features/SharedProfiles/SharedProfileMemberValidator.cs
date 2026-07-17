using FluentValidation.Results;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.SharedProfiles;

internal static class SharedProfileMemberValidator
{
    internal const int MinMembers = 2;
    internal const int MaxMembers = 8;

    internal static async Task EnsureValidMembersAsync(
        IApplicationDbContext context,
        IReadOnlyList<Guid> memberIds,
        Guid actingUserId,
        CancellationToken cancellationToken)
    {
        if (memberIds.Count is < MinMembers or > MaxMembers)
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(memberIds), $"Group must have between {MinMembers} and {MaxMembers} members.")
            ]);
        }

        var validCount = await context.Users
            .AsNoTracking()
            .CountAsync(u => memberIds.Contains(u.Id) && u.IsActive && u.PeerServerId == null, cancellationToken);

        if (validCount != memberIds.Count)
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(memberIds), "One or more members are invalid.")
            ]);
        }

        var blocked = await SharedProfilePreferencesHelper.GetUsersBlockingMembershipAsync(context, memberIds, cancellationToken);
        if (blocked.Any(id => id != actingUserId))
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(memberIds), "One or more users do not allow being added to shared profiles.")
            ]);
        }
    }

    /// <summary>
    /// Loads the shared profile and ensures the acting user is either the host or an administrator.
    /// Intended for host-only management operations (playback policy, home layout, restriction profile, playlists).
    /// </summary>
    internal static async Task<SharedProfile> GetGroupForHostAsync(
        IApplicationDbContext context,
        IIdentityService identityService,
        Guid groupId,
        Guid userId,
        string? identityId,
        CancellationToken cancellationToken)
    {
        var group = await context.SharedProfiles
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

        Guard.Against.NotFound(groupId, group);

        var isHost = group.HostUserId == userId;
        var isAdmin = !string.IsNullOrEmpty(identityId)
            && await identityService.IsInRoleAsync(identityId, Roles.Administrator);

        if (!isHost && !isAdmin)
            throw new ForbiddenAccessException();

        return group;
    }

    internal static async Task<SharedProfile> GetGroupForMemberAsync(
        IApplicationDbContext context,
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var group = await context.SharedProfiles
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

        Guard.Against.NotFound(groupId, group);

        if (!group.Members.Any(m => m.UserId == userId))
            throw new ForbiddenAccessException();

        return group;
    }
}
