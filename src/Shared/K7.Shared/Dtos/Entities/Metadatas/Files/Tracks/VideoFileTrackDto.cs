using K7.Server.Domain.Entities.Metadatas.Files.Tracks;

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

    public static VideoFileTrackDto FromDomain(VideoFileTrack domain) => new()
    {
        Index = domain.Index,
        IsDefault = domain.IsDefault,
        BitDepth = domain.BitDepth,
        Codec = domain.Codec,
        Height = domain.Height,
        Level = domain.Level,
        PixelFormat = domain.PixelFormat,
        Profile = domain.Profile,
        Width = domain.Width
    };
}
