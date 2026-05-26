namespace K7.Shared.Dtos.Requests;

public sealed record DeleteAccountRequest
{
    public string? CurrentPassword { get; init; }
}
