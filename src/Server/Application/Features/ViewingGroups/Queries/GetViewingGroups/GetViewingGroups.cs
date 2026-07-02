using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.ViewingGroups;

namespace K7.Server.Application.Features.ViewingGroups.Queries.GetViewingGroups;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetViewingGroupsQuery : IRequest<IReadOnlyList<ViewingGroupDto>>;

public class GetViewingGroupsQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetViewingGroupsQuery, IReadOnlyList<ViewingGroupDto>>
{
    public async Task<IReadOnlyList<ViewingGroupDto>> Handle(GetViewingGroupsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return [];

        var groups = await context.ViewingGroups
            .AsNoTracking()
            .Include(g => g.Members)
            .Where(g => g.Members.Any(m => m.UserId == userId))
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

        if (groups.Count == 0)
            return [];

        var memberUserIds = groups.SelectMany(g => g.Members).Select(m => m.UserId).Distinct().ToList();
        var userInfo = await ViewingGroupUserInfoLoader.LoadAsync(context, memberUserIds, cancellationToken);

        return groups
            .Select(g => g.ToViewingGroupDto(userInfo.DisplayNames, userInfo.IdentityUserIds, userInfo.AvatarUrls))
            .ToList();
    }
}

internal static class ViewingGroupUserInfoLoader
{
    internal sealed record UserInfo(
        IReadOnlyDictionary<Guid, string?> DisplayNames,
        IReadOnlyDictionary<Guid, string?> IdentityUserIds,
        IReadOnlyDictionary<Guid, string?> AvatarUrls);

    internal static async Task<UserInfo> LoadAsync(
        IApplicationDbContext context,
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
            return new UserInfo(new Dictionary<Guid, string?>(), new Dictionary<Guid, string?>(), new Dictionary<Guid, string?>());

        var users = await context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.IdentityUserId })
            .ToListAsync(cancellationToken);

        var avatarMap = await context.MetadataPictures
            .AsNoTracking()
            .Where(p => p.UserId != null && userIds.Contains(p.UserId.Value) && p.Type == MetadataPictureType.UserAvatar)
            .Select(p => new { p.UserId, p.Id })
            .ToDictionaryAsync(p => p.UserId!.Value, p => (string?)$"/api/metadata-pictures/{p.Id}", cancellationToken);

        var displayNames = users.ToDictionary(u => u.Id, u => u.DisplayName);
        var identityUserIds = users.ToDictionary(u => u.Id, u => u.IdentityUserId);

        return new UserInfo(displayNames, identityUserIds, avatarMap);
    }
}
