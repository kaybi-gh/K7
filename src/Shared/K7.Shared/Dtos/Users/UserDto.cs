using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Users;

public sealed record UserDto
{
    public required Guid Id { get; init; }
    public required string? IdentityUserId { get; init; }
    public required string? Email { get; init; }
    public required string? UserName { get; init; }
    public required string Role { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsGuest { get; init; }
    public required bool HasPin { get; init; }
    public string? PinHash { get; init; }
    public required IReadOnlyList<CapabilityOverrideDto> CapabilityOverrides { get; init; }
    public required IReadOnlyList<Guid> ExcludedLibraryIds { get; init; }
    public required IReadOnlyList<Guid> ExcludedMediaIds { get; init; }
    public Guid? ContentRestrictionProfileId { get; init; }

    public static UserDto FromDomain(User domain, bool includePinHash = false) => new()
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
}

public sealed record CapabilityOverrideDto
{
    public required Capability Capability { get; init; }
    public required bool Enabled { get; init; }
}
