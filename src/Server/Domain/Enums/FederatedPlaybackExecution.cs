namespace K7.Server.Domain.Enums;

/// <summary>
/// Who runs remux/transcode for federated playback. Assigned by the library owner (source).
/// </summary>
public enum FederatedPlaybackExecution
{
    /// <summary>Source server runs ffmpeg. Requester proxies HLS or direct-stream.</summary>
    Origin,

    /// <summary>Requester runs ffmpeg against the origin direct-stream URL.</summary>
    Peer
}
