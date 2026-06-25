namespace K7.Clients.Shared.Models;

public enum IntelligentSearchKind
{
    Sonic,
    Lyrics
}

public sealed record IntelligentSearchRequest(IntelligentSearchKind Kind, string Query);
