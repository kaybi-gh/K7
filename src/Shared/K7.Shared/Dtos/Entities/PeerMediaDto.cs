using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record PeerMediaDto
{
    public required Guid Id { get; init; }
    public required MediaType Type { get; init; }
    public string? Title { get; init; }
    public string? OriginalTitle { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public IReadOnlyList<PeerExternalIdDto> ExternalIds { get; init; } = [];
    public IReadOnlyList<PeerFileDto> Files { get; init; } = [];
    public IReadOnlyList<string> Genres { get; init; } = [];
}

public sealed record PeerExternalIdDto
{
    public required string Provider { get; init; }
    public required string Value { get; init; }
}

public sealed record PeerFileDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required long Size { get; init; }
}
