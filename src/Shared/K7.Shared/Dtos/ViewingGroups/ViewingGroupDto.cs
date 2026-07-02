namespace K7.Shared.Dtos.ViewingGroups;

public sealed record ViewingGroupMemberDto
{
    public required Guid UserId { get; init; }
    public string? IdentityUserId { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
}

public sealed record ViewingGroupDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid HostUserId { get; init; }
    public string? HostIdentityUserId { get; init; }
    public required bool HasPin { get; init; }
    public string? PinHash { get; init; }
    public required IReadOnlyList<ViewingGroupMemberDto> Members { get; init; }
}

public sealed record ViewingGroupMemberCandidateDto
{
    public required Guid Id { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
}
