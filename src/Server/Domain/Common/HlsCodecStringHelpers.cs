using System.Globalization;
using System.Text;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;

namespace K7.Server.Domain.Common;

/// <summary>
/// Helpers to generate HLS codec strings according to
/// <a href="https://datatracker.ietf.org/doc/html/rfc6381#section-3.3">RFC 6381 section 3.3</a>
/// and the <a href="https://mp4ra.org">MP4 Registration Authority</a>.
/// </summary>
public static class HlsCodecStringHelpers
{
    private const string H264_BASELINE = ".42E0";
    private const string H264_MAIN = ".4D40";
    private const string H264_HIGH = ".6400";
    private const string H264_DEFAULT = ".4240"; // Constrained baseline

    public const string MP3 = "mp4a.40.34";
    public const string AC3 = "ac-3";
    public const string EAC3 = "ec-3";
    public const string FLAC = "fLaC";
    public const string ALAC = "alac";
    public const string OPUS = "Opus";

    public static string GetAACString(string? profile)
    {
        return new StringBuilder("mp4a", 9)
            .Append(profile?.ToLower() == "he" ? ".40.5" : ".40.2")
            .ToString();
    }

    public static string GetH264String(string? profile, int level)
    {
        string profileString = profile?.ToLower() switch
        {
            "high" => H264_HIGH,
            "main" => H264_MAIN,
            "baseline" => H264_BASELINE,
            _ => H264_DEFAULT
        };

        return new StringBuilder("avc1", 11)
            .Append(profileString)
            .Append(level.ToString("X2", CultureInfo.InvariantCulture))
            .ToString();
    }

    public static string GetH265String(string? profile, int level)
    {
        StringBuilder result = new("hvc1", 16);
        string profileString = profile?.ToLower() switch
        {
            "main10" => ".2.4",
            _ => ".1.4"
        };

        return result.Append(profileString)
            .Append(".L")
            .Append(level)
            .Append(".B0")
            .ToString();
    }

    public static string GetVp9String(int width, int height, string pixelFormat, float framerate, int bitDepth)
    {
        string profileString = pixelFormat switch
        {
            "yuv420p" => "00",
            "yuvj420p" => "00",
            "yuv422p" => "01",
            "yuv444p" => "01",
            "yuv420p10le" => "02",
            "yuv420p12le" => "02",
            "yuv422p10le" => "03",
            "yuv422p12le" => "03",
            "yuv444p10le" => "03",
            "yuv444p12le" => "03",
            _ => "00"
        };

        var lumaPictureSize = width * height;
        var lumaSampleRate = lumaPictureSize * framerate;
        string levelString = lumaPictureSize switch
        {
            <= 36864 => "10",
            <= 73728 => "11",
            <= 122880 => "20",
            <= 245760 => "21",
            <= 552960 => "30",
            <= 983040 => "31",
            <= 2228224 => lumaSampleRate <= 83558400 ? "40" : "41",
            <= 8912896 => lumaSampleRate <= 311951360 ? "50" : (lumaSampleRate <= 588251136 ? "51" : "52"),
            <= 35651584 => lumaSampleRate <= 1176502272 ? "60" : (lumaSampleRate <= 4706009088 ? "61" : "62"),
            _ => "00"
        };

        bitDepth = (bitDepth is 8 or 10 or 12) ? bitDepth : 8;

        return new StringBuilder("vp09", 13)
            .Append('.').Append(profileString)
            .Append('.').Append(levelString)
            .Append('.').Append(bitDepth.ToString("D2", CultureInfo.InvariantCulture))
            .ToString();
    }

    public static string GetAv1String(string? profile, int level, bool tierFlag, int bitDepth)
    {
        StringBuilder result = new("av01", 13);
        string profileString = profile?.ToLower() switch
        {
            "main" => ".0",
            "high" => ".1",
            "professional" => ".2",
            _ => ".0"
        };

        result.Append(profileString);

        level = level is > 0 and <= 31 ? level : 19;
        bitDepth = (bitDepth is 8 or 10 or 12) ? bitDepth : 8;

        result.Append('.').AppendFormat(CultureInfo.InvariantCulture, "{0:D2}", level)
            .Append(tierFlag ? 'H' : 'M')
            .Append('.').Append(bitDepth.ToString("D2", CultureInfo.InvariantCulture));

        return result.ToString();
    }

    public static string GetHlsCodecs(VideoFileMetadata videoFileMetadata)
    {
        var videoTrack = videoFileMetadata.VideoTracks
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Index)
            .FirstOrDefault();

        var audioTrack = videoFileMetadata.AudioTracks
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Index)
            .FirstOrDefault();

        var videoCodec = GetVideoCodecString(videoTrack);
        var audioCodec = GetAudioCodecString(audioTrack);

        return (videoCodec, audioCodec) switch
        {
            (not null, not null) => $"{videoCodec},{audioCodec}",
            (not null, null) => videoCodec,
            (null, not null) => audioCodec,
            _ => string.Empty
        };
    }

    public static string GetHlsCodecs(string? videoCodec, string? audioCodec)
    {
        var video = GetVideoCodecString(videoCodec);
        var audio = GetAudioCodecString(audioCodec);

        return (video, audio) switch
        {
            (not null, not null) => $"{video},{audio}",
            (not null, null) => video,
            (null, not null) => audio,
            _ => string.Empty
        };
    }

    private static string? GetVideoCodecString(VideoFileTrack? track)
    {
        if (track == null || string.IsNullOrWhiteSpace(track.Codec))
        {
            return null;
        }

        return GetVideoCodecString(track.Codec, track.Profile, track.Level, track.Width, track.Height, track.PixelFormat, track.BitDepth);
    }

    private static string? GetVideoCodecString(string? codec)
    {
        if (string.IsNullOrWhiteSpace(codec))
        {
            return null;
        }

        var normalized = codec.ToLowerInvariant();

        return normalized switch
        {
            "h264" or "avc" or "avc1" => GetH264String(null, 40),
            "hevc" or "h265" or "hev1" or "hvc1" => GetH265String(null, 4),
            "vp9" => GetVp9String(1920, 1080, "yuv420p", 30, 8),
            "av1" => GetAv1String(null, 19, false, 8),
            _ => codec
        };
    }

    private static string? GetVideoCodecString(string codec, string? profile, int level, int width, int height, string? pixelFormat, int? bitDepth)
    {
        var normalized = codec.ToLowerInvariant();

        return normalized switch
        {
            "h264" or "avc" or "avc1" => GetH264String(profile, level),
            "hevc" or "h265" or "hev1" or "hvc1" => GetH265String(profile, level),
            "vp9" => GetVp9String(width, height, pixelFormat ?? "yuv420p", 30, bitDepth ?? 8),
            "av1" => GetAv1String(profile, level, false, bitDepth ?? 8),
            _ => codec
        };
    }

    private static string? GetAudioCodecString(AudioFileTrack? track)
    {
        if (track == null || string.IsNullOrWhiteSpace(track.Codec))
        {
            return null;
        }

        return GetAudioCodecString(track.Codec, track.Profile);
    }

    private static string? GetAudioCodecString(string? codec, string? profile = null)
    {
        if (string.IsNullOrWhiteSpace(codec))
        {
            return null;
        }

        var normalized = codec.ToLowerInvariant();

        return normalized switch
        {
            "aac" or "mp4a" => GetAACString(profile),
            "mp3" => MP3,
            "ac3" or "ac-3" => AC3,
            "eac3" or "e-ac-3" or "ec-3" => EAC3,
            "flac" => FLAC,
            "alac" => ALAC,
            "opus" => OPUS,
            _ => codec
        };
    }
}
