using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities.Medias;

public sealed record LiteMusicArtistDto : LiteMediaDto
{
    public MusicArtistType ArtistType { get; init; }
    public string? Country { get; init; }
}
