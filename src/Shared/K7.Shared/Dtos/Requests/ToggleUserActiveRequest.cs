namespace K7.Shared.Dtos.Requests;

public sealed record ToggleUserActiveRequest
{
    public required bool IsActive { get; init; }
}
