namespace K7.Server.Domain.Entities.Metadatas.External;

public class ExternalMusicArtistDetails
{
    public string? Biography { get; init; }
    public string? ImageUrl { get; init; }
    public string? Country { get; init; }
    public string? MusicBrainzArtistId { get; init; }
    public string? WikidataId { get; init; }
    public IReadOnlyList<ExternalMusicArtistMember>? Members { get; init; }
}

public class ExternalMusicArtistMember
{
    public required string Name { get; init; }
    public string? MusicBrainzArtistId { get; init; }
    public string? Role { get; init; }
    public bool IsActive { get; init; } = true;
}
