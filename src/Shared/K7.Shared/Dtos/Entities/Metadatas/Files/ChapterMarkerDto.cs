using K7.Server.Domain.Entities.Metadatas.Files;

namespace K7.Shared.Dtos.Entities.Metadatas.Files;

public sealed record ChapterMarkerDto
{
    public double StartSeconds { get; init; }
    public double? EndSeconds { get; init; }
    public string? Title { get; init; }
}
