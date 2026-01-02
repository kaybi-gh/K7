namespace K7.Shared.Dtos.Entities.Medias;

public sealed record MovieDto : MediaDto
{
    public string? TagLine { get; init; }
    public string? Overview { get; init; }
    public string? OriginalLanguage { get; init; }
}
