namespace K7.Server.Application.Common.Interfaces;

public record AudioMuseAiConnectionResult(bool Success, string? Version = null, string? Error = null);

public interface IAudioMuseAiService
{
    Task<AudioMuseAiConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default);
    Task<List<Guid>> GetSimilarTracksAsync(Guid trackId, int count = 20, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetSonicPathAsync(Guid fromId, Guid toId, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetSuggestionsAsync(IEnumerable<Guid> recentTrackIds, int count = 20, CancellationToken cancellationToken = default);
    Task<List<Guid>> CreateSmartPlaylistAsync(string prompt, int count = 30, CancellationToken cancellationToken = default);
}
