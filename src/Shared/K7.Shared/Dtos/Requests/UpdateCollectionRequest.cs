namespace K7.Shared.Dtos.Requests;

using K7.Server.Domain.Enums;

public sealed record UpdateCollectionRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public bool IsPublic { get; init; }
    public VisibilityScope VisibilityScope { get; init; } = VisibilityScope.Nobody;
}
