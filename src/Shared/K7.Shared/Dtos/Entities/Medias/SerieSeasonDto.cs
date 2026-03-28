namespace K7.Shared.Dtos.Entities.Medias;

public sealed record SerieSeasonDto : MediaDto
{
    public int SeasonNumber { get; init; }
    public string? Overview { get; init; }
    public Guid SerieId { get; init; }
    public string? SerieTitle { get; init; }
    public IReadOnlyList<LiteSerieEpisodeDto>? Episodes { get; init; }
}
