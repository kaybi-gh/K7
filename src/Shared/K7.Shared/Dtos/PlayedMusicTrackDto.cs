using K7.Shared.Dtos.Entities.Medias;

namespace K7.Shared.Dtos;

public sealed record PlayedMusicTrackDto
{
    public required LiteMusicTrackDto Track { get; init; }
    public int PlayCount { get; init; }
}
