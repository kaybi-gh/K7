namespace K7.Shared.Dtos.Entities.Medias;

public sealed record MovieDto : MediaDto
{
    public string? TagLine { get; init; }
    public string? Overview { get; init; }
    public string? OriginalLanguage { get; init; }
    public string? ContentRating { get; init; }
    public long? Budget { get; init; }
    public long? Revenue { get; init; }
    public IReadOnlyList<string>? Studios { get; init; }
    public IReadOnlyList<TrailerDto>? Trailers { get; init; }
}
