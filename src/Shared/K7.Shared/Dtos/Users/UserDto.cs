using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Users;

public sealed record UserDto
{
    public required Guid Id { get; init; }
    public string? IdentityUserId { get; init; }
    public string? Email { get; init; }
    public required string? UserName { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public required string Role { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsGuest { get; init; }
    public required bool HasPin { get; init; }
    public string? PinHash { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }
    public required IReadOnlyList<CapabilityOverrideDto> CapabilityOverrides { get; init; }
    public required IReadOnlyList<UserLibraryExclusionDto> LibraryExclusions { get; init; }
    public required IReadOnlyList<UserMediaExclusionDto> MediaExclusions { get; init; }
    public Guid? ContentRestrictionProfileId { get; init; }
}

public sealed record CapabilityOverrideDto
{
    public required Capability Capability { get; init; }
    public required bool Enabled { get; init; }
}
