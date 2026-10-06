namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Detects Video.js VHS failures when remuxed fMP4 audio segments cannot be appended to MSE.
/// Common when HLS audio is stream-copied from MPEG-TS (DVB recordings).
/// </summary>
public static class HlsAudioRemuxRecovery
{
    public static bool IsRemuxAudioAppendFailure(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return message.Contains("audio append", StringComparison.OrdinalIgnoreCase);
    }
}
