namespace K7.Shared.Dtos;

public sealed record DirectoryContentDto
{
    public required string Path { get; init; }
    public required IReadOnlyList<DirectoryEntryDto> Directories { get; init; }
}

public sealed record DirectoryEntryDto
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
}
