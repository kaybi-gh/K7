namespace K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

public sealed record VideoFileTrackDto : FileTrackDto
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required string Codec { get; init; }
    public required string Profile { get; init; }
    public required int Level { get; init; }
    public string? PixelFormat { get; init; }
    public int? BitDepth { get; init; }

}
