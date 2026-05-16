using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities.Medias;

public sealed record MusicArtistDto : MediaDto
{
    public MusicArtistType ArtistType { get; init; }
    public string? Biography { get; init; }
    public string? Country { get; init; }
    public IReadOnlyList<MusicAlbumDto>? Albums { get; init; }
}
