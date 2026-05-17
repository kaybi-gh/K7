namespace K7.Shared.Dtos.Entities.Medias;

public sealed record MusicArtistCreditDto
{
    public Guid ArtistId { get; init; }
    public required string ArtistName { get; init; }
    public bool IsGuest { get; init; }
}
