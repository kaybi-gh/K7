namespace K7.Shared.Dtos.Requests;

public sealed record InitiatePeeringRequest
{
    public required string RemoteUrl { get; init; }
}
