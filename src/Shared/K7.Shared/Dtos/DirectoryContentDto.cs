namespace K7.Shared.Dtos;

public sealed record DirectoryContentDto
{
    public required string Path { get; init; }

    /// <summary>
    /// Parent directory path, or null when at a browse root (show drive/root listing).
    /// </summary>
    public string? ParentPath { get; init; }

    public required IReadOnlyList<DirectoryEntryDto> Directories { get; init; }
}

public sealed record DirectoryEntryDto
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
}
