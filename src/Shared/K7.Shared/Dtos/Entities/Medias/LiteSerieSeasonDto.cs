namespace K7.Shared.Dtos.Entities.Medias;

public sealed record LiteSerieSeasonDto
{
    public Guid Id { get; init; }
    public int SeasonNumber { get; init; }
    public string? Title { get; init; }
    public int EpisodeCount { get; init; }
    public MetadataPictureDto? Poster { get; init; }
}
