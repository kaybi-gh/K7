namespace K7.Shared.Dtos.Requests;

public sealed record UpdateEmailRequest
{
    public required string Email { get; init; }
    public required string CurrentPassword { get; init; }
}
