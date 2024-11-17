using System.Globalization;
using System.Text;
using FFMpegCore;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;
public record CreateFileMetadatasCommand : IRequest
{
    public required Guid Id { get; set; }
    public required FileType FileType { get; set; }
}

public class CreateFileMetadatasCommandHandler : IRequestHandler<CreateFileMetadatasCommand>
{
    private readonly IApplicationDbContext _context;

    public CreateFileMetadatasCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(CreateFileMetadatasCommand request, CancellationToken cancellationToken)
    {        
        var indexedFile = await _context.IndexedFiles
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, indexedFile);
        Guard.Against.NullOrEmpty(indexedFile.Path);

        var file = new FileInfo(indexedFile.Path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("File not found.", indexedFile.Path);
        }

        var mediaAnalysis = await FFProbe.AnalyseAsync(indexedFile.Path, cancellationToken: cancellationToken);

        if (mediaAnalysis.PrimaryVideoStream == null)
        {
            throw new InvalidOperationException();
        }

        var fileMetadata = request.FileType switch
        {
            FileType.Audio => throw new NotImplementedException(),
            FileType.Video => new VideoFileMetadata()
            {
                Id = Guid.NewGuid(),
                Duration = mediaAnalysis.Duration,
                VideoBitrate = mediaAnalysis.PrimaryVideoStream.BitRate,
                VideoResolution = Qualities.Video
                    .Any(x => x.Value.Height == mediaAnalysis.PrimaryVideoStream.Height ||
                        x.Value.Width == mediaAnalysis.PrimaryVideoStream.Width) ?
                    Qualities.Video.First(x => 
                        x.Value.Height == mediaAnalysis.PrimaryVideoStream.Height ||
                        x.Value.Width == mediaAnalysis.PrimaryVideoStream.Width).Key
                    : throw new InvalidOperationException(),
                AudioTracks = ExtractAudioTracksFromMediaAnalysis(mediaAnalysis),
                VideoTracks = ExtractVideoTracksFromMediaAnalysis(mediaAnalysis)
            },
            _ => throw new NotImplementedException(),
        };

        await _context.FileMetadatas.AddAsync(fileMetadata, cancellationToken);
        indexedFile.FileMetadata = fileMetadata;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static List<AudioFileTrack> ExtractAudioTracksFromMediaAnalysis(IMediaAnalysis mediaAnalysis)
    {
        bool hasDefaultAudio = mediaAnalysis.AudioStreams.Any(s => s.Disposition?.Any(d => d.Key == "default" && d.Value) ?? false);
        return mediaAnalysis.AudioStreams.Select(x => new AudioFileTrack()
        {
            Index = x.Index,
            IsDefault = IsDefaultTrack(hasDefaultAudio, x.Disposition, x.Index),
            Language = x.Language,
            Name = x.Tags?.FirstOrDefault(x => x.Key == "title").Value ?? x.Language,
            CodecName = x.CodecName,
            Channels = x.Channels,
            ChannelLayout = x.ChannelLayout,
            Profile = x.Profile,
            SampleRateHz = x.SampleRateHz
        }).ToList();
    }

    private static List<VideoFileTrack> ExtractVideoTracksFromMediaAnalysis(IMediaAnalysis mediaAnalysis)
    {
        bool hasDefaultVideo = mediaAnalysis.VideoStreams.Any(s => s.Disposition?.Any(d => d.Key == "default" && d.Value) ?? false);
        return mediaAnalysis.VideoStreams.Select(x => new VideoFileTrack
        {
            CodecName = x.CodecName,
            Width = x.Width,
            Height = x.Height,
            Profile = x.Profile,
            Index = x.Index,
            BitDepth = x.BitDepth,
            Level = 0, // TODO - Migrate to Xabe or PR FFMpegCore
            IsDefault = IsDefaultTrack(hasDefaultVideo, x.Disposition, x.Index),
            PixelFormat = x.PixelFormat
        }).ToList();
    }

    private static bool IsDefaultTrack(bool hasDefaultTrack, IDictionary<string, bool>? disposition, int index)
    {
        return hasDefaultTrack ?
            disposition?.Any(x => x.Key == "default" && x.Value) ?? false
            : index == 0;
    }
}

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

    /// <summary>
    /// Gets an AAC codec string.
    /// </summary>
    /// <param name="profile">AAC profile.</param>
    /// <returns>AAC codec string.</returns>
    public static string GetAACString(string? profile)
    {
        return new StringBuilder("mp4a", 9)
            .Append(profile?.ToLower() == "he" ? ".40.5" : ".40.2") // Default to LC profile
            .ToString();
    }

    /// <summary>
    /// Gets a H.264 codec string.
    /// </summary>
    /// <param name="profile">H.264 profile.</param>
    /// <param name="level">H.264 level.</param>
    /// <returns>H.264 string.</returns>
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

    /// <summary>
    /// Gets a H.265 codec string.
    /// </summary>
    /// <param name="profile">H.265 profile.</param>
    /// <param name="level">H.265 level.</param>
    /// <returns>H.265 string.</returns>
    public static string GetH265String(string? profile, int level)
    {
        StringBuilder result = new StringBuilder("hvc1", 16);
        string profileString = profile?.ToLower() switch
        {
            "main10" => ".2.4",
            _ => ".1.4"  // Default to main profile
        };

        return result.Append(profileString)
            .Append(".L")
            .Append(level)
            .Append(".B0")
            .ToString();
    }

    /// <summary>
    /// Gets a VP9 codec string.
    /// </summary>
    /// <param name="width">Video width.</param>
    /// <param name="height">Video height.</param>
    /// <param name="pixelFormat">Video pixel format.</param>
    /// <param name="framerate">Video framerate.</param>
    /// <param name="bitDepth">Video bitDepth.</param>
    /// <returns>The VP9 codec string.</returns>
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

    /// <summary>
    /// Gets an AV1 codec string.
    /// </summary>
    /// <param name="profile">AV1 profile.</param>
    /// <param name="level">AV1 level.</param>
    /// <param name="tierFlag">AV1 tier flag.</param>
    /// <param name="bitDepth">AV1 bit depth.</param>
    /// <returns>The AV1 codec string.</returns>
    public static string GetAv1String(string? profile, int level, bool tierFlag, int bitDepth)
    {
        StringBuilder result = new StringBuilder("av01", 13);
        string profileString = profile?.ToLower() switch
        {
            "main" => ".0",
            "high" => ".1",
            "professional" => ".2",
            _ => ".0" // Default to Main profile
        };

        result.Append(profileString);

        level = (level is > 0 and <= 31) ? level : 19; // Default to level 6.3

        bitDepth = (bitDepth is 8 or 10 or 12) ? bitDepth : 8; // Default to 8 bits

        result.Append('.').AppendFormat(CultureInfo.InvariantCulture, "{0:D2}", level)
            .Append(tierFlag ? 'H' : 'M')
            .Append('.').Append(bitDepth.ToString("D2", CultureInfo.InvariantCulture));

        return result.ToString();
    }

    public static string GetHlsCodecs(VideoFileMetadata videoFileMetadata)
    {
        return "";
        // TODO - Compute HlsCodec
    }
}
