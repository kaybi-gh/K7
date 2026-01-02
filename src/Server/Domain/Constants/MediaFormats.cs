using K7.Server.Domain.Entities.MediaFormats;

namespace K7.Server.Domain.Constants;

public static partial class Constants
{
    public static readonly IEnumerable<BaseMediaFormat> MediaFormats =
    [
        new AudioMediaFormat()
        {
            Id = "audio-mp3-mp3",
            Container = "mp3",
            Codec = "mp3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mp4-aac",
            Container = "mp4",
            Codec = "aac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mp4-mp3",
            Container = "mp4",
            Codec = "mp3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-webm-opus",
            Container = "webm",
            Codec = "opus"
        },
        new AudioMediaFormat()
        {
            Id = "audio-webm-vorbis",
            Container = "webm",
            Codec = "vorbis"
        },
        new AudioMediaFormat()
        {
            Id = "audio-ogg-vorbis",
            Container = "ogg",
            Codec = "vorbis"
        },
        new AudioMediaFormat()
        {
            Id = "audio-flac-flac",
            Container = "flac",
            Codec = "flac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-wav-pcm",
            Container = "wav",
            Codec = "pcm"
        },
        new AudioMediaFormat()
        {
            Id = "audio-aac-aac",
            Container = "aac",
            Codec = "aac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-asf-wma",
            Container = "asf",
            Codec = "wma"
        },
        new AudioMediaFormat()
        {
            Id = "audio-ape-ape",
            Container = "ape",
            Codec = "ape"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mpegts-aac",
            Container = "mpegts",
            Codec = "aac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mpegts-ac3",
            Container = "mpegts",
            Codec = "ac3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mpegts-eac3",
            Container = "mpegts",
            Codec = "eac3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mpegts-opus",
            Container = "mpegts",
            Codec = "opus"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-aac-h264",
            Container = "matroska",
            AudioCodec = "aac",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-aac-hevc",
            Container = "matroska",
            AudioCodec = "aac",
            VideoCodec = "hevc"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-aac-vp9",
            Container = "matroska",
            AudioCodec = "aac",
            VideoCodec = "vp9"
        },
        new VideoMediaFormat()
        {
            Id = "video-mp4-aac-h264",
            Container = "mp4",
            AudioCodec = "aac",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-mp4-aac-hevc",
            Container = "mp4",
            AudioCodec = "aac",
            VideoCodec = "hevc"
        },
        new VideoMediaFormat()
        {
            Id = "video-mp4-aac-vp9",
            Container = "mp4",
            AudioCodec = "aac",
            VideoCodec = "vp9"
        },
        new VideoMediaFormat()
        {
            Id = "video-webm-vorbis-vp9",
            Container = "webm",
            AudioCodec = "vorbis",
            VideoCodec = "vp9"
        },
        new VideoMediaFormat()
        {
            Id = "video-webm-opus-vp9",
            Container = "webm",
            AudioCodec = "opus",
            VideoCodec = "vp9"
        },
        new VideoMediaFormat()
        {
            Id = "video-avi-mp3-mpeg4",
            Container = "avi",
            AudioCodec = "mp3",
            VideoCodec = "mpeg4"
        },
        new VideoMediaFormat()
        {
            Id = "video-avi-aac-mpeg4",
            Container = "avi",
            AudioCodec = "aac",
            VideoCodec = "mpeg4"
        },
        new VideoMediaFormat()
        {
            Id = "video-avi-mp3-h264",
            Container = "avi",
            AudioCodec = "mp3",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-avi-aac-h264",
            Container = "avi",
            AudioCodec = "aac",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-mov-aac-h264",
            Container = "mov",
            AudioCodec = "aac",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-mov-aac-hevc",
            Container = "mov",
            AudioCodec = "aac",
            VideoCodec = "hevc"
        },
        new VideoMediaFormat()
        {
            Id = "video-flv-aac-h264",
            Container = "flv",
            AudioCodec = "aac",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-avi-ac3-mpeg2",
            Container = "avi",
            AudioCodec = "ac3",
            VideoCodec = "mpeg2"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-eac3-h264",
            Container = "matroska",
            AudioCodec = "eac3",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-mp4-eac3-h264",
            Container = "mp4",
            AudioCodec = "eac3",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-mp4-eac3-hevc",
            Container = "mp4",
            AudioCodec = "eac3",
            VideoCodec = "hevc"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-opus-vp9",
            Container = "matroska",
            AudioCodec = "opus",
            VideoCodec = "vp9"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-flac-h264",
            Container = "matroska",
            AudioCodec = "flac",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-mpegts-h264",
            Container = "mpegts",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-mpegts-hevc",
            Container = "mpegts",
            VideoCodec = "hevc"
        },
        new VideoMediaFormat()
        {
            Id = "video-mpegts-vp9",
            Container = "mpegts",
            VideoCodec = "vp9"
        }
    ];
}
