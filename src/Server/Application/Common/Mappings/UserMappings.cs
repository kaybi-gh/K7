using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos.Users;

namespace K7.Server.Application.Common.Mappings;

public static class UserMappings
{
    extension(User domain)
    {
        public UserDto ToUserDto(bool includePinHash = false) => new()
        {
            Id = domain.Id,
            IdentityUserId = domain.IdentityUserId,
            Email = domain.Email,
            UserName = domain.UserName,
            Role = domain.Role,
            Created = domain.Created,
            IsActive = domain.IsActive,
            IsGuest = domain.Role == Roles.Guest,
            HasPin = domain.PinHash is not null,
            PinHash = includePinHash ? domain.PinHash : null,
            CapabilityOverrides = domain.CapabilityOverrides
                .Select(o => new CapabilityOverrideDto
                {
                    Capability = o.Capability,
                    Enabled = o.Enabled
                }).ToList(),
            ExcludedLibraryIds = domain.LibraryExclusions
                .Select(e => e.LibraryId).ToList(),
            ExcludedMediaIds = domain.MediaExclusions
                .Select(e => e.MediaId).ToList(),
            ContentRestrictionProfileId = domain.ContentRestrictionProfileId
        };

        public LiteUserDto ToLiteUserDto() => new()
        {
            Id = domain.Id,
            IdentityUserId = domain.IdentityUserId
        };
    }
}
