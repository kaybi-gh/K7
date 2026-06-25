namespace K7.Shared.Interfaces;

public interface IMusicIntelligenceClientService
{
    Task<List<Guid>> GetSimilarTracksAsync(Guid trackId, int count = 20, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetSonicPathAsync(Guid fromId, Guid toId, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetSuggestionsAsync(IEnumerable<Guid> recentTrackIds, int count = 20, CancellationToken cancellationToken = default);
    Task<List<Guid>> CreateSmartPlaylistAsync(string prompt, int count = 30, CancellationToken cancellationToken = default);
    Task<List<Guid>> SearchTracksBySonicTextAsync(string query, int count = 50, CancellationToken cancellationToken = default);
    Task<List<Guid>> SearchTracksByLyricsAsync(string query, int count = 50, CancellationToken cancellationToken = default);
}
