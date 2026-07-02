namespace K7.Shared.Dtos.Requests;

public sealed record CreateSharedProfileRequest
{
    public required string Name { get; init; }
    public required Guid HostUserId { get; init; }
    public required IReadOnlyList<Guid> MemberUserIds { get; init; }
    public string? Pin { get; init; }
}

public sealed record UpdateSharedProfileRequest
{
    public string? Name { get; init; }
    public Guid? HostUserId { get; init; }
    public IReadOnlyList<Guid>? MemberUserIds { get; init; }
}

public sealed record SetSharedProfilePinRequest
{
    public string? Pin { get; init; }
}

public sealed record LeaveSharedProfileRequest
{
    public Guid? NewHostUserId { get; init; }
}
