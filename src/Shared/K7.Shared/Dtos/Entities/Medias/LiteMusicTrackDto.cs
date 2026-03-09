namespace K7.Shared.Dtos.Entities.Medias;

public sealed record LiteMusicTrackDto : LiteMediaDto
{
    public Guid AlbumId { get; init; }
    public int? TrackNumber { get; init; }
}
