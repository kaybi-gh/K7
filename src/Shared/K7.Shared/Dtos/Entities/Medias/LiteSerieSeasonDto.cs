namespace K7.Shared.Dtos.Entities.Medias;

public sealed record LiteSerieSeasonDto : LiteMediaDto
{
    public Guid SerieId { get; init; }
    public string? SerieTitle { get; init; }
    public int SeasonNumber { get; init; }
    public int EpisodeCount { get; init; }
    public MetadataPictureDto? Poster { get; init; }
    public IReadOnlyList<MetadataPictureDto>? SeriePictures { get; init; }
}
