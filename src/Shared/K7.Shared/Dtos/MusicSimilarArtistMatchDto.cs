namespace K7.Shared.Dtos;

public sealed record MusicSimilarArtistMatchDto
{
    public string? ArtistId { get; init; }
    public string? Artist { get; init; }
    public double? Divergence { get; init; }
}
