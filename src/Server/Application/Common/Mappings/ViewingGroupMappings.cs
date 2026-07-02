using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos.ViewingGroups;

namespace K7.Server.Application.Common.Mappings;

public static class ViewingGroupMappings
{
    extension(ViewingGroup group)
    {
        public ViewingGroupDto ToViewingGroupDto(
            IReadOnlyDictionary<Guid, string?> displayNames,
            IReadOnlyDictionary<Guid, string?> identityUserIds,
            IReadOnlyDictionary<Guid, string?> avatarUrls,
            bool includePinHash = true) => new()
            {
                Id = group.Id,
                Name = group.Name,
                HostUserId = group.HostUserId,
                HostIdentityUserId = identityUserIds.GetValueOrDefault(group.HostUserId),
                HasPin = group.PinHash is not null,
                PinHash = includePinHash ? group.PinHash : null,
                Members = group.Members
                .OrderBy(m => m.UserId == group.HostUserId ? 0 : 1)
                .Select(m => new ViewingGroupMemberDto
                {
                    UserId = m.UserId,
                    IdentityUserId = identityUserIds.GetValueOrDefault(m.UserId),
                    DisplayName = displayNames.GetValueOrDefault(m.UserId),
                    AvatarUrl = avatarUrls.GetValueOrDefault(m.UserId)
                })
                .ToList()
            };
    }
}
