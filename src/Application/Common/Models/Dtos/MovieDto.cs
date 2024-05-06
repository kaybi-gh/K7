namespace MediaServer.Application.Common.Models.Dtos;

public record MovieDto : MediaDto
{
    public string? TagLine { get; init; }
    public string? Overview { get; init; }
    public string? OriginalLanguage { get; init; }
}
