namespace K7.Shared.Dtos.Requests;

public sealed record ResetUserPasswordRequest
{
    public required string NewPassword { get; init; }
}
