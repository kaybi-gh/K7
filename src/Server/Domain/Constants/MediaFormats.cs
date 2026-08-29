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
        new AudioMediaFormat()
        {
            Id = "audio-matroska-aac",
            Container = "matroska",
            Codec = "aac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-matroska-ac3",
            Container = "matroska",
            Codec = "ac3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-matroska-eac3",
            Container = "matroska",
            Codec = "eac3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-matroska-opus",
            Container = "matroska",
            Codec = "opus"
        },
        new AudioMediaFormat()
        {
            Id = "audio-matroska-flac",
            Container = "matroska",
            Codec = "flac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mp4-eac3",
            Container = "mp4",
            Codec = "eac3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mov-aac",
            Container = "mov",
            Codec = "aac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-avi-mp3",
            Container = "avi",
            Codec = "mp3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-avi-aac",
            Container = "avi",
            Codec = "aac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-avi-ac3",
            Container = "avi",
            Codec = "ac3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-flv-aac",
            Container = "flv",
            Codec = "aac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-matroska-dts",
            Container = "matroska",
            Codec = "dts"
        },
        new AudioMediaFormat()
        {
            Id = "audio-matroska-truehd",
            Container = "matroska",
            Codec = "truehd"
        },
        new AudioMediaFormat()
        {
            Id = "audio-matroska-mp3",
            Container = "matroska",
            Codec = "mp3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-matroska-pcm",
            Container = "matroska",
            Codec = "pcm"
        },
        new AudioMediaFormat()
        {
            Id = "audio-matroska-vorbis",
            Container = "matroska",
            Codec = "vorbis"
        },
        new AudioMediaFormat()
        {
            Id = "audio-matroska-mp2",
            Container = "matroska",
            Codec = "mp2"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mp4-ac3",
            Container = "mp4",
            Codec = "ac3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mp4-dts",
            Container = "mp4",
            Codec = "dts"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mp4-opus",
            Container = "mp4",
            Codec = "opus"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mov-ac3",
            Container = "mov",
            Codec = "ac3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mov-eac3",
            Container = "mov",
            Codec = "eac3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mpegts-mp3",
            Container = "mpegts",
            Codec = "mp3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mpegts-dts",
            Container = "mpegts",
            Codec = "dts"
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
            Id = "video-matroska-ac3-h264",
            Container = "matroska",
            AudioCodec = "ac3",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-ac3-hevc",
            Container = "matroska",
            AudioCodec = "ac3",
            VideoCodec = "hevc"
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
            Id = "video-matroska-eac3-hevc",
            Container = "matroska",
            AudioCodec = "eac3",
            VideoCodec = "hevc"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-dts-h264",
            Container = "matroska",
            AudioCodec = "dts",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-dts-hevc",
            Container = "matroska",
            AudioCodec = "dts",
            VideoCodec = "hevc"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-truehd-hevc",
            Container = "matroska",
            AudioCodec = "truehd",
            VideoCodec = "hevc"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-flac-hevc",
            Container = "matroska",
            AudioCodec = "flac",
            VideoCodec = "hevc"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-aac-av1",
            Container = "matroska",
            AudioCodec = "aac",
            VideoCodec = "av1"
        },
        new VideoMediaFormat()
        {
            Id = "video-mp4-aac-av1",
            Container = "mp4",
            AudioCodec = "aac",
            VideoCodec = "av1"
        },
        new VideoMediaFormat()
        {
            Id = "video-webm-opus-av1",
            Container = "webm",
            AudioCodec = "opus",
            VideoCodec = "av1"
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
        },
        new AudioMediaFormat()
        {
            Id = "audio-ogg-opus",
            Container = "ogg",
            Codec = "opus"
        },
        new AudioMediaFormat()
        {
            Id = "audio-ogg-flac",
            Container = "ogg",
            Codec = "flac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mp4-flac",
            Container = "mp4",
            Codec = "flac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mp4-alac",
            Container = "mp4",
            Codec = "alac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mp4-truehd",
            Container = "mp4",
            Codec = "truehd"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mp4-pcm",
            Container = "mp4",
            Codec = "pcm"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mov-mp3",
            Container = "mov",
            Codec = "mp3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mov-alac",
            Container = "mov",
            Codec = "alac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mov-dts",
            Container = "mov",
            Codec = "dts"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mov-flac",
            Container = "mov",
            Codec = "flac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-matroska-alac",
            Container = "matroska",
            Codec = "alac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mpegts-mp2",
            Container = "mpegts",
            Codec = "mp2"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mpegts-flac",
            Container = "mpegts",
            Codec = "flac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-avi-eac3",
            Container = "avi",
            Codec = "eac3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-avi-dts",
            Container = "avi",
            Codec = "dts"
        },
        new AudioMediaFormat()
        {
            Id = "audio-m4v-aac",
            Container = "m4v",
            Codec = "aac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-m4v-ac3",
            Container = "m4v",
            Codec = "ac3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-m4v-eac3",
            Container = "m4v",
            Codec = "eac3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-3gp-aac",
            Container = "3gp",
            Codec = "aac"
        },
        new AudioMediaFormat()
        {
            Id = "audio-aiff-pcm",
            Container = "aiff",
            Codec = "pcm"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-vp8",
            Container = "matroska",
            VideoCodec = "vp8"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-mpeg2",
            Container = "matroska",
            VideoCodec = "mpeg2"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-mpeg4",
            Container = "matroska",
            VideoCodec = "mpeg4"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-opus-h264",
            Container = "matroska",
            AudioCodec = "opus",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-opus-hevc",
            Container = "matroska",
            AudioCodec = "opus",
            VideoCodec = "hevc"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-mp3-h264",
            Container = "matroska",
            AudioCodec = "mp3",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-vorbis-h264",
            Container = "matroska",
            AudioCodec = "vorbis",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-mp4-mpeg4",
            Container = "mp4",
            VideoCodec = "mpeg4"
        },
        new VideoMediaFormat()
        {
            Id = "video-mp4-mpeg2",
            Container = "mp4",
            VideoCodec = "mpeg2"
        },
        new VideoMediaFormat()
        {
            Id = "video-mp4-ac3-h264",
            Container = "mp4",
            AudioCodec = "ac3",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-mp4-ac3-hevc",
            Container = "mp4",
            AudioCodec = "ac3",
            VideoCodec = "hevc"
        },
        new VideoMediaFormat()
        {
            Id = "video-webm-vp8",
            Container = "webm",
            VideoCodec = "vp8"
        },
        new VideoMediaFormat()
        {
            Id = "video-mpegts-mpeg2",
            Container = "mpegts",
            VideoCodec = "mpeg2"
        },
        new VideoMediaFormat()
        {
            Id = "video-mpegts-av1",
            Container = "mpegts",
            VideoCodec = "av1"
        },
        new VideoMediaFormat()
        {
            Id = "video-mpegts-aac-h264",
            Container = "mpegts",
            AudioCodec = "aac",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-mpegts-ac3-h264",
            Container = "mpegts",
            AudioCodec = "ac3",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-avi-hevc",
            Container = "avi",
            VideoCodec = "hevc"
        },
        new VideoMediaFormat()
        {
            Id = "video-mov-mpeg4",
            Container = "mov",
            VideoCodec = "mpeg4"
        },
        new VideoMediaFormat()
        {
            Id = "video-mov-ac3-h264",
            Container = "mov",
            AudioCodec = "ac3",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-m4v-h264",
            Container = "m4v",
            VideoCodec = "h264"
        },
        new VideoMediaFormat()
        {
            Id = "video-m4v-hevc",
            Container = "m4v",
            VideoCodec = "hevc"
        },
        new VideoMediaFormat()
        {
            Id = "video-3gp-h264",
            Container = "3gp",
            VideoCodec = "h264"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mpeg-mp2",
            Container = "mpeg",
            Codec = "mp2"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mpeg-mp3",
            Container = "mpeg",
            Codec = "mp3"
        },
        new AudioMediaFormat()
        {
            Id = "audio-mpeg-ac3",
            Container = "mpeg",
            Codec = "ac3"
        },
        new VideoMediaFormat()
        {
            Id = "video-mpeg-mpeg2",
            Container = "mpeg",
            VideoCodec = "mpeg2"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-av1",
            Container = "matroska",
            VideoCodec = "av1"
        },
        new VideoMediaFormat()
        {
            Id = "video-mp4-av1",
            Container = "mp4",
            VideoCodec = "av1"
        },
        new VideoMediaFormat()
        {
            Id = "video-webm-av1",
            Container = "webm",
            VideoCodec = "av1"
        },
        new VideoMediaFormat()
        {
            Id = "video-mov-av1",
            Container = "mov",
            VideoCodec = "av1"
        },
        new VideoMediaFormat()
        {
            Id = "video-m4v-av1",
            Container = "m4v",
            VideoCodec = "av1"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-opus-av1",
            Container = "matroska",
            AudioCodec = "opus",
            VideoCodec = "av1"
        },
        new VideoMediaFormat()
        {
            Id = "video-webm-vorbis-av1",
            Container = "webm",
            AudioCodec = "vorbis",
            VideoCodec = "av1"
        },
        new VideoMediaFormat()
        {
            Id = "video-matroska-av2",
            Container = "matroska",
            VideoCodec = "av2"
        },
        new VideoMediaFormat()
        {
            Id = "video-mp4-av2",
            Container = "mp4",
            VideoCodec = "av2"
        },
        new VideoMediaFormat()
        {
            Id = "video-webm-av2",
            Container = "webm",
            VideoCodec = "av2"
        }
    ];
}
