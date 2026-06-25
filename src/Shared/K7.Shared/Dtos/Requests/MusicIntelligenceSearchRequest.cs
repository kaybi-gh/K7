namespace K7.Shared.Dtos.Requests;

public sealed record MusicIntelligenceSearchRequest
{
    public required string Query { get; init; }
    public int Count { get; init; } = 50;
}
