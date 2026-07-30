namespace K7.Clients.Shared.Models;

public enum IntelligentSearchKind
{
    Sonic,
    Lyrics,
    SimilarArtists
}

public sealed record IntelligentSearchRequest(
    IntelligentSearchKind Kind,
    string Query,
    Guid? SeedId = null);
