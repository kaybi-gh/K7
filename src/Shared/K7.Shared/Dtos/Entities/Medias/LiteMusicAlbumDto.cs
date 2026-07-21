namespace K7.Shared.Dtos.Entities.Medias;

public sealed record LiteMusicAlbumDto : LiteMediaDto
{
    public Guid? ArtistId { get; init; }
    public string? ArtistName { get; init; }
}
