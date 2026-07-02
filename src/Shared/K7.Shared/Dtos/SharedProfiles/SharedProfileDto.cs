namespace K7.Shared.Dtos.SharedProfiles;

public sealed record SharedProfileMemberDto
{
    public required Guid UserId { get; init; }
    public string? IdentityUserId { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
}

public sealed record SharedProfileDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid HostUserId { get; init; }
    public string? HostIdentityUserId { get; init; }
    public required bool HasPin { get; init; }
    public string? PinHash { get; init; }
    public required IReadOnlyList<SharedProfileMemberDto> Members { get; init; }
}

public sealed record SharedProfileMemberCandidateDto
{
    public required Guid Id { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
}
