using K7.Shared.Dtos;

namespace K7.Server.Application.Common.Interfaces;

public record MusicIntelligenceConnectionResult(bool Success, string? Version = null, string? Error = null);

public interface IMusicIntelligenceService
{
    Task<MusicIntelligenceConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task<List<Guid>> GetSimilarTracksAsync(Guid trackId, int count = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MusicMoodPresetDto>> GetMoodPresetsAsync(CancellationToken cancellationToken = default);
    Task<List<Guid>> GetMoodTracksAsync(string moodKey, int centroidIndex, int count = 50, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetDiscoveryTracksAsync(int count = 50, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetSonicPathAsync(Guid fromId, Guid toId, CancellationToken cancellationToken = default);
    Task<List<Guid>> CreatePlaylistFromPromptAsync(string prompt, int count = 30, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MusicSimilarArtistMatchDto>> GetSimilarArtistsAsync(
        Guid artistId,
        string? artistName,
        int count = 12,
        CancellationToken cancellationToken = default);
    Task<List<Guid>> SearchTracksBySonicTextAsync(string query, int count = 50, CancellationToken cancellationToken = default);
    Task<List<Guid>> SearchTracksByLyricsAsync(string query, int count = 50, CancellationToken cancellationToken = default);
}
