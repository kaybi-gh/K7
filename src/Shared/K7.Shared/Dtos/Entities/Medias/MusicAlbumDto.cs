namespace K7.Shared.Dtos.Entities.Medias;

public sealed record MusicAlbumDto : MediaDto
{
    public string? Overview { get; init; }
    public IEnumerable<LiteMusicTrackDto>? Tracks { get; init; }
}
