namespace K7.Shared.Dtos.Requests;

public sealed record UpdateProfileRequest
{
    public string? DisplayName { get; init; }
}
