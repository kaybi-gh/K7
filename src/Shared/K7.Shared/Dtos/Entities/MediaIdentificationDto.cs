namespace K7.Shared.Dtos.Entities;

public sealed record MediaIdentificationDto
{
    public required string Title { get; init; }
    public DateOnly? ReleaseYear { get; init; }

    public int? TrackNumber { get; init; }
    public string? AlbumName { get; init; }
    public string? ArtistName { get; init; }

    public string? SeriesTitle { get; init; }
    public int? SeasonNumber { get; init; }
    public int? EpisodeNumber { get; init; }
    public int? AbsoluteNumber { get; init; }
}
