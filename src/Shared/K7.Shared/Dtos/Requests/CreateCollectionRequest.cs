using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public sealed record CreateCollectionRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public bool IsPublic { get; init; }
    public MediaType? MediaType { get; init; }
}
