namespace K7.Shared.Dtos.Entities.Medias;

public sealed record SerieDto : MediaDto
{
    public string? Overview { get; init; }
    public string? Status { get; init; }
    public string? OriginalLanguage { get; init; }
    public string? ContentRating { get; init; }
    public string? Network { get; init; }
    public IReadOnlyList<LiteSerieSeasonDto>? Seasons { get; init; }
}
