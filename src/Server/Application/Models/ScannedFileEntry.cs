namespace K7.Server.Application.Models;

public sealed record ScannedFileEntry
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public string? ParentDirectory { get; init; }
    public required long Size { get; init; }
    public required DateTimeOffset LastWriteTimeUtc { get; init; }
}
