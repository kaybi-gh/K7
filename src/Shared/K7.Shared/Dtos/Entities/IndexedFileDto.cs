using K7.Shared.Dtos.Entities.Metadatas.Files;

namespace K7.Shared.Dtos.Entities;

public sealed record IndexedFileDto
{
    public required Guid Id { get; init; }
    public required Guid LibraryId { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required string Path { get; init; }
    public string? ParentDirectory { get; init; }
    public required uint Hash { get; init; }
    public required long Size { get; init; }

    public MediaIdentificationDto? Identification { get; init; }
    public FileMetadataDto? FileMetadata { get; set; }
}
