namespace K7.Shared.Dtos.Entities.Medias;

public sealed record LiteSerieEpisodeDto : LiteMediaDto
{
    public int EpisodeNumber { get; init; }
    public int SeasonNumber { get; init; }
    public int SerieSeasonCount { get; init; }
    public double? Duration { get; init; }
    public Guid SerieId { get; init; }
    public string? SerieTitle { get; init; }
    public Guid? StillImageId { get; init; }
    public IReadOnlyList<MetadataPictureDto>? SeriePictures { get; init; }
    public IReadOnlyList<MetadataPictureDto>? SeasonPictures { get; init; }
}
