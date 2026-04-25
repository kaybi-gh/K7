namespace K7.Shared.Dtos.Requests;

public sealed record UpdateCollectionRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public bool IsPublic { get; init; }
}
