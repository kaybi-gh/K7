using AwesomeAssertions;
using K7.Clients.MAUI.Playback;

namespace K7.Clients.MAUI.SmokeTests;

[TestFixture]
public class VlcAuthProxyHlsMasterTests
{
    [Test]
    public void StripHlsSubtitleRenditions_ShouldRemoveSubtitleMediaAndStreamInfAttribute()
    {
        var master =
            """
            #EXTM3U
            #EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="audio",NAME="FR",DEFAULT=YES,URI="audio/1/index.m3u8"
            #EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID="subs",NAME="sub-3",DEFAULT=YES,URI="subtitles/3/index.m3u8"
            #EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID="subs",NAME="sub-4",URI="subtitles/4/index.m3u8"
            #EXT-X-STREAM-INF:BANDWIDTH=5000000,CODECS="avc1.4d401f",AUDIO="audio",SUBTITLES="subs"
            video/720p/index.m3u8
            """;

        var stripped = VlcAuthProxy.StripHlsSubtitleRenditions(master);

        stripped.Should().Contain("TYPE=AUDIO");
        stripped.Should().Contain("video/720p/index.m3u8");
        stripped.Should().NotContain("TYPE=SUBTITLES");
        stripped.Should().NotContain("SUBTITLES=");
        stripped.Should().Contain("AUDIO=\"audio\"");
    }

    [Test]
    public void RewriteHlsMasterForLibVlc_ShouldServeVideoOnlyAndCapturePlayUrls()
    {
        var master =
            """
            #EXTM3U
            #EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="audio",NAME="FR",DEFAULT=YES,URI="audio/1/index.m3u8?streamSessionId=abc"
            #EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="audio",NAME="EN",URI="audio/2/index.m3u8?streamSessionId=abc"
            #EXT-X-STREAM-INF:BANDWIDTH=5000000,CODECS="avc1.4d401f,mp4a.40.2",AUDIO="audio"
            video/720p/index.m3u8?streamSessionId=abc
            """;

        var rewritten = VlcAuthProxy.RewriteHlsMasterForLibVlc(
            master,
            "http://127.0.0.1:9/hls/",
            out var audioSlave,
            out var videoPlay,
            preferredAudioTrackIndex: 1);

        rewritten.Should().Contain("#EXT-X-VERSION:7");
        rewritten.Should().Contain("#EXT-X-INDEPENDENT-SEGMENTS");
        rewritten.Should().Contain("CLOSED-CAPTIONS=NONE");
        rewritten.Should().NotContain("TYPE=AUDIO");
        rewritten.Should().NotContain("AUDIO=");
        rewritten.Should().NotContain("mp4a.");
        rewritten.Should().Contain("CODECS=\"avc1.4d401f\"");
        rewritten.Should().Contain("video/720p/index.m3u8?streamSessionId=abc");
        videoPlay.Should().Be("http://127.0.0.1:9/hls/video/720p/index.m3u8?streamSessionId=abc");
        audioSlave.Should().Be("http://127.0.0.1:9/hls/audio/1/index.m3u8?streamSessionId=abc");
        // Absolute STREAM-INF for desktop VLC / re-fetch.
        rewritten.Should().Contain("http://127.0.0.1:9/hls/video/720p/index.m3u8?streamSessionId=abc");
    }

    [Test]
    public void BuildAudioOnlyMasterPlaylist_ShouldPointStreamInfAtAudioMedia()
    {
        var master = VlcAuthProxy.BuildAudioOnlyMasterPlaylist(
            "http://127.0.0.1:9/hls/audio/1/index.m3u8?streamSessionId=abc");

        master.Should().Contain("#EXT-X-STREAM-INF:");
        master.Should().NotContain("CODECS=");
        master.Should().Contain("http://127.0.0.1:9/hls/audio/1/index.m3u8?streamSessionId=abc");
    }
}
