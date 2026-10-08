using K7.Server.Domain.Entities;
using K7.Shared.Dtos;

namespace K7.Server.Application.Common.Interfaces;

/// <summary>
/// In-memory state for requester-side federated playback (Peer execution).
/// </summary>
public interface IFederatedPlaybackSessionStore
{
    void Set(FederatedPlaybackSessionState state);

    FederatedPlaybackSessionState? Get(Guid localSessionId);

    void Remove(Guid localSessionId);
}

public sealed class FederatedPlaybackSessionState
{
    public required Guid LocalSessionId { get; init; }
    public required Guid RemoteSessionId { get; init; }
    public required Guid PeerServerId { get; init; }
    public required Guid RemoteIndexedFileId { get; init; }
    public required string OriginDirectStreamUrl { get; init; }
    public string? LocalMediaPath { get; set; }
    public StreamDecisionDto? StreamDecision { get; set; }
    public TimeSpan? Duration { get; set; }
    public IReadOnlyList<HlsSegment>? Segments { get; set; }
    public bool UsedEqualLengthFallback { get; set; }

    /// <summary>
    /// Resume offset from the master playlist query (startSeconds). Propagated to media
    /// playlists so ABR quality switches keep EXT-X-START instead of restarting at 0.
    /// </summary>
    public double? StartSeconds { get; set; }

    /// <summary>
    /// Player-selected audio stream from DefaultAudioTrackIndex. Wins over a missing
    /// decision index so the master does not advertise stream 0 (often the video).
    /// </summary>
    public int? RequestedAudioTrackIndex { get; set; }
}
