namespace K7.Shared.Dtos.Entities.Medias;

public sealed record SerieEpisodeDto : MediaDto
{
    public int EpisodeNumber { get; init; }
    public int SeasonNumber { get; init; }
    public string? Overview { get; init; }
    public DateOnly? AirDate { get; init; }
    public int? Runtime { get; init; }
    public Guid SerieId { get; init; }
    public Guid SeasonId { get; init; }
    public string? SerieTitle { get; init; }
    public string? SeasonTitle { get; init; }
}
