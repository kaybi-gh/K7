using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos.Users;

namespace K7.Server.Application.Common.Mappings;

public static class UserMappings
{
    extension(User domain)
    {
        public UserDto ToUserDto(bool includePinHash = false, string? avatarUrl = null) => new()
        {
            Id = domain.Id,
            IdentityUserId = domain.IdentityUserId,
            Email = domain.Email,
            UserName = domain.UserName,
            DisplayName = domain.DisplayName,
            AvatarUrl = avatarUrl,
            Role = domain.Role,
            Created = domain.Created,
            IsActive = domain.IsActive,
            IsGuest = domain.Role == Roles.Guest,
            HasPin = domain.PinHash is not null,
            PinHash = includePinHash ? domain.PinHash : null,
            DeletedAt = domain.DeletedAt,
            CapabilityOverrides = domain.CapabilityOverrides
                .Select(o => new CapabilityOverrideDto
                {
                    Capability = o.Capability,
                    Enabled = o.Enabled
                }).ToList(),
            LibraryExclusions = domain.LibraryExclusions
                .Select(e => new UserLibraryExclusionDto
                {
                    LibraryId = e.LibraryId,
                    IsAdminExcluded = e.IsAdminExcluded,
                    IsSelfExcluded = e.IsSelfExcluded
                }).ToList(),
            MediaExclusions = domain.MediaExclusions
                .Select(e => new UserMediaExclusionDto
                {
                    MediaId = e.MediaId,
                    IsAdminExcluded = e.IsAdminExcluded,
                    IsSelfExcluded = e.IsSelfExcluded
                }).ToList(),
            ContentRestrictionProfileId = domain.ContentRestrictionProfileId
        };

        public LiteUserDto ToLiteUserDto() => new()
        {
            Id = domain.Id,
            IdentityUserId = domain.IdentityUserId
        };
    }
}
