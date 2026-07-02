namespace K7.Shared.Dtos.Requests;

public sealed record CreateViewingGroupRequest
{
    public required string Name { get; init; }
    public required Guid HostUserId { get; init; }
    public required IReadOnlyList<Guid> MemberUserIds { get; init; }
    public string? Pin { get; init; }
}

public sealed record UpdateViewingGroupRequest
{
    public string? Name { get; init; }
    public Guid? HostUserId { get; init; }
    public IReadOnlyList<Guid>? MemberUserIds { get; init; }
}

public sealed record SetViewingGroupPinRequest
{
    public string? Pin { get; init; }
}
