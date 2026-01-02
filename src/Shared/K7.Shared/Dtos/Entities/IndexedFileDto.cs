using K7.Server.Domain.Entities;
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
    public bool IsSplitPart { get; init; }
    public bool IsComposite { get; init; }

    public FileMetadataDto? FileMetadata { get; set; }

    public static IndexedFileDto FromDomain(IndexedFile domain) => new()
    {
        Id = domain.Id,
        Name = domain.Name,
        LibraryId = domain.LibraryId,
        Extension = domain.Extension,
        Path = domain.Path,
        ParentDirectory = domain.ParentDirectory,
        Hash = domain.Hash,
        Size = domain.Size,
        IsSplitPart = domain.IsSplitPart,
        IsComposite = domain.IsComposite,
        FileMetadata = domain.FileMetadata != null ? FileMetadataDto.FromDomain(domain.FileMetadata) : null
    };
}
