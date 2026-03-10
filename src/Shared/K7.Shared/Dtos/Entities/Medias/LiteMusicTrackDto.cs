namespace K7.Shared.Dtos.Entities.Medias;

public sealed record LiteMusicTrackDto : LiteMediaDto
{
    public Guid AlbumId { get; init; }
    public int? TrackNumber { get; init; }
    public Guid? IndexedFileId { get; init; }
    public double? Duration { get; init; }
    public string? AlbumTitle { get; init; }
    public string? ArtistName { get; init; }
}
