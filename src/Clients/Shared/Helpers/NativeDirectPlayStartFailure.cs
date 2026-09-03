namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Native MediaElement / ExoPlayer start failures that should walk
/// Direct Play -&gt; remux -&gt; transcode, or keep the overlay usable.
/// </summary>
public static class NativeDirectPlayStartFailure
{
    public static bool ShouldFallbackQualityLadder(
        string? errorDetail,
        double positionSeconds,
        bool isLocalFile)
    {
        if (isLocalFile)
            return false;

        if (positionSeconds > 2)
            return false;

        return LooksLikeDecoderOrRuntimeCheck(errorDetail);
    }

    /// <summary>
    /// Amlogic HEVC/DV + EAC3 Direct Play often 1004s once at decoder init, then
    /// plays on a second Prepare. Retry the same URL before remux or abort.
    /// </summary>
    public static bool ShouldRetrySameDirectPlay(
        string? errorDetail,
        double positionSeconds,
        bool isLocalFile,
        bool isDirectPlay,
        int retryCount)
    {
        if (!isDirectPlay || retryCount >= 1)
            return false;

        return ShouldFallbackQualityLadder(errorDetail, positionSeconds, isLocalFile);
    }

    public static bool LooksLikeDecoderOrRuntimeCheck(string? errorDetail)
    {
        if (string.IsNullOrEmpty(errorDetail))
            return false;

        return Contains(errorDetail, "ERROR_CODE_FAILED_RUNTIME_CHECK")
            || Contains(errorDetail, "ERROR_CODE_DECODER_INIT_FAILED")
            || Contains(errorDetail, "ERROR_CODE_DECODER_QUERY_FAILED")
            || Contains(errorDetail, "ERROR_CODE_DECODING_FAILED")
            || Contains(errorDetail, "ERROR_CODE_DECODING_FORMAT_EXCEEDS_CAPABILITIES")
            || Contains(errorDetail, "ERROR_CODE_DECODING_FORMAT_UNSUPPORTED")
            || Contains(errorDetail, "ERROR_CODE_AUDIO_TRACK_INIT_FAILED")
            || Contains(errorDetail, "ERROR_CODE_AUDIO_TRACK_WRITE_FAILED")
            || Contains(errorDetail, "PlayerErrorCode=1004");
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
