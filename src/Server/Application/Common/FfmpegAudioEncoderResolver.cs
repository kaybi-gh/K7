namespace K7.Server.Application.Common;

public static class FfmpegAudioEncoderResolver
{
    public static string? ResolveEncoderName(string logicalCodec)
    {
        if (string.IsNullOrWhiteSpace(logicalCodec))
            return null;

        return logicalCodec.ToLowerInvariant() switch
        {
            "aac" => "aac",
            "opus" => "libopus",
            "mp3" => "libmp3lame",
            "vorbis" => "libvorbis",
            "ac3" => "ac3",
            "eac3" => "eac3",
            "flac" => "flac",
            "alac" => "alac",
            _ => logicalCodec
        };
    }
}
