namespace K7.Server.Domain.Common;

/// <summary>
/// Output channel count for HLS audio encode (AAC):
/// never more channels than the source, never more than the device output can render
/// (<see cref="AudioOutputChannelTokens"/>), never more than the encoder accepts, and only
/// layouts HLS clients decode reliably (1, 2, 6, 8). 5.0 gets a silent LFE (6), 7.0 gets 8,
/// 3.0 / 4.0 downmix to stereo. Announce N in the master (CHANNELS) and deliver N.
/// </summary>
public static class HlsAudioChannelPolicy
{
    /// <summary>
    /// Built-in ffmpeg aac and libopus take 8; libfdk_aac stops at 6 (clamped in the transcoder).
    /// </summary>
    public const int DefaultEncoderMaxChannels = 8;

    public static int Resolve(int sourceChannels, int? deviceMaxChannels, int encoderMaxChannels = DefaultEncoderMaxChannels)
    {
        var channels = sourceChannels > 0 ? sourceChannels : 2;

        if (deviceMaxChannels is > 0 && deviceMaxChannels < channels)
            channels = deviceMaxChannels.Value;

        if (encoderMaxChannels > 0 && encoderMaxChannels < channels)
            channels = encoderMaxChannels;

        return channels switch
        {
            <= 2 => channels,
            5 or 6 => 6,
            7 or >= 8 => 8,
            // 3.0 / 4.0: no HLS layout, downmix to stereo for compatibility.
            _ => 2
        };
    }
}
