namespace K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

public sealed record SubtitleFileTrackDto : FileTrackDto
{
    public string? Name { get; init; }
    public string? Language { get; init; }
    public string? Codec { get; init; }
    public bool IsTextBased { get; init; }
    public bool IsForced { get; init; }
    public bool IsHearingImpaired { get; init; }
}
