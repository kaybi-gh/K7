namespace MediaClient.Shared.Services.MediaServer.Dtos;

public record IndexedFileDto
{
    public required Guid LibraryId { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required string Path { get; init; }
    public string? ParentDirectory { get; init; }
    public required string Hash { get; init; }
    public required long Size { get; init; }
    public bool IsSplitPart { get; init; }
    public bool IsComposite { get; init; }
    public Uri? DirectStreamUri { get; init; }
    public Uri? HlsStreamUri { get; init; }
}
