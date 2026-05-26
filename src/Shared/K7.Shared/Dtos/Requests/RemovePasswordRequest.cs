namespace K7.Shared.Dtos.Requests;

public sealed record RemovePasswordRequest
{
    public required string CurrentPassword { get; init; }
}
