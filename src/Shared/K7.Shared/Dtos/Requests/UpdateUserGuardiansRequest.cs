namespace K7.Shared.Dtos.Requests;

public sealed record UpdateUserGuardiansRequest
{
    public required IReadOnlyList<Guid> GuardianUserIds { get; init; }
}
