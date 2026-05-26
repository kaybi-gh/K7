namespace K7.Shared.Dtos.Requests;

public sealed record SetPasswordRequest
{
    public required string NewPassword { get; init; }
}
