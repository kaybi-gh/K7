using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record PeerLibraryDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required LibraryMediaType MediaType { get; init; }
    public int MediaCount { get; init; }
}
