namespace K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

public sealed record VideoFileTrackDto : FileTrackDto
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public string? Codec { get; init; }
    public string? Profile { get; init; }
    public required int Level { get; init; }
    public string? PixelFormat { get; init; }
    public int? BitDepth { get; init; }
    public float? FrameRate { get; init; }
}
