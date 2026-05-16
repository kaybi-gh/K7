namespace K7.Shared.Dtos.Entities.Medias;

public sealed record MusicAlbumDto : MediaDto
{
    public string? Overview { get; init; }
    public Guid? ArtistId { get; init; }
    public string? ArtistName { get; init; }
    public IReadOnlyList<LiteMusicTrackDto>? Tracks { get; init; }
}
