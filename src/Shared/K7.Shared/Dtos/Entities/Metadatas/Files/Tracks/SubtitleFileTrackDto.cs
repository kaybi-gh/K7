using K7.Server.Domain.Entities.Metadatas.Files.Tracks;

namespace K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

public sealed record SubtitleFileTrackDto : FileTrackDto
{
    public string? Name { get; init; }
    public string? Language { get; init; }
    public required string Codec { get; init; }
    public bool IsTextBased { get; init; }
    public bool IsForced { get; init; }

    public static SubtitleFileTrackDto FromDomain(SubtitleFileTrack domain) => new()
    {
        Index = domain.Index,
        IsDefault = domain.IsDefault,
        Name = domain.Name,
        Language = domain.Language,
        Codec = domain.Codec,
        IsTextBased = domain.IsTextBased,
        IsForced = domain.IsForced
    };
}
