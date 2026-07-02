using FluentValidation.Results;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Users;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.ViewingGroups;

internal static class ViewingGroupMemberValidator
{
    internal const int MinMembers = 2;
    internal const int MaxMembers = 8;

    internal static async Task EnsureValidMembersAsync(
        IApplicationDbContext context,
        IReadOnlyList<Guid> memberIds,
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
    }

    internal static async Task<ViewingGroup> GetGroupForMemberAsync(
        IApplicationDbContext context,
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var group = await context.ViewingGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

        Guard.Against.NotFound(groupId, group);

        if (!group.Members.Any(m => m.UserId == userId))
            throw new ForbiddenAccessException();

        return group;
    }
}
