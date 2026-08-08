namespace K7.Server.Domain.Interfaces;

/// <summary>
/// Progressive (non-HLS) audio transcode for OpenSubsonic /rest/stream clients.
/// </summary>
public interface IOpenSubsonicAudioTranscoder
{
    /// <summary>
    /// Starts ffmpeg and returns a readable stdout stream. Disposing the stream kills ffmpeg.
    /// </summary>
    Stream OpenProgressiveTranscode(
        string inputFilePath,
        string format,
        int bitrateKbps,
        double timeOffsetSeconds);
}
