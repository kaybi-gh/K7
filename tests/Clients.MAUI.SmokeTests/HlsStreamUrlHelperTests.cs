using K7.Clients.MAUI.Platforms.Windows;

namespace K7.Clients.MAUI.SmokeTests;

[TestFixture]
public class HlsStreamUrlHelperTests
{
    [Test]
    public void IsK7StreamResource_ShouldReturnFalse_WhenUrlIsNullOrEmpty()
    {
        HlsStreamUrlHelper.IsK7StreamResource(null).Should().BeFalse();
        HlsStreamUrlHelper.IsK7StreamResource("").Should().BeFalse();
    }

    [Test]
    public void IsK7StreamResource_ShouldReturnTrue_WhenUrlContainsHlsStreamPath()
    {
        HlsStreamUrlHelper.IsK7StreamResource("https://host/api/hls-stream/abc/manifest.m3u8")
            .Should().BeTrue();
    }

    [Test]
    public void IsK7StreamResource_ShouldReturnTrue_WhenUrlContainsDirectStreamPath()
    {
        HlsStreamUrlHelper.IsK7StreamResource("https://host/api/files/1/direct-stream")
            .Should().BeTrue();
    }

    [Test]
    public void IsK7StreamResource_ShouldReturnTrue_WhenUrlContainsRemoteStreamSessionsPath()
    {
        HlsStreamUrlHelper.IsK7StreamResource("https://host/api/remote-stream-sessions/xyz")
            .Should().BeTrue();
    }

    [Test]
    public void IsK7StreamResource_ShouldReturnTrue_WhenUrlContainsSubtitleVttPath()
    {
        HlsStreamUrlHelper.IsK7StreamResource(
                "https://host/api/indexed-files/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/subtitles/2.vtt")
            .Should().BeTrue();
    }

    [Test]
    public void IsK7StreamResource_ShouldReturnFalse_WhenUrlIsUnrelated()
    {
        HlsStreamUrlHelper.IsK7StreamResource("https://host/api/media/1").Should().BeFalse();
    }

    [Test]
    public void AbsolutizeManifestUrls_ShouldReturnEmpty_WhenManifestIsEmpty()
    {
        var result = HlsStreamUrlHelper.AbsolutizeManifestUrls("", new Uri("https://host/hls/master.m3u8"));

        result.Should().BeEmpty();
    }

    [Test]
    public void AbsolutizeManifestUrls_ShouldAbsolutizeRelativeSegmentLines()
    {
        var manifest = """
            #EXTM3U
            #EXTINF:4.0,
            0.m4s
            #EXTINF:4.0,
            1.m4s
            """;
        var baseUrl = new Uri("https://host/api/hls-stream/job/playlist.m3u8");

        var result = HlsStreamUrlHelper.AbsolutizeManifestUrls(manifest, baseUrl);

        result.Should().Contain("https://host/api/hls-stream/job/0.m4s");
        result.Should().Contain("https://host/api/hls-stream/job/1.m4s");
        result.Should().Contain("#EXTINF:4.0,");
    }

    [Test]
    public void AbsolutizeManifestUrls_ShouldLeaveAbsoluteUrisUnchanged()
    {
        var manifest = """
            #EXTM3U
            #EXTINF:4.0,
            https://cdn.example/seg/0.m4s
            """;
        var baseUrl = new Uri("https://host/api/hls-stream/job/playlist.m3u8");

        var result = HlsStreamUrlHelper.AbsolutizeManifestUrls(manifest, baseUrl);

        result.Should().Contain("https://cdn.example/seg/0.m4s");
    }

    [Test]
    public void AbsolutizeManifestUrls_ShouldAbsolutizeUriAttributesInTagLines()
    {
        var manifest = """
            #EXTM3U
            #EXT-X-MAP:URI="init.m4s"
            #EXT-X-KEY:METHOD=AES-128,URI="key.bin"
            """;
        var baseUrl = new Uri("https://host/api/hls-stream/job/playlist.m3u8");

        var result = HlsStreamUrlHelper.AbsolutizeManifestUrls(manifest, baseUrl);

        result.Should().Contain("URI=\"https://host/api/hls-stream/job/init.m4s\"");
        result.Should().Contain("URI=\"https://host/api/hls-stream/job/key.bin\"");
    }

    [Test]
    public void EnsureWindowsHlsAudioTranscodeQuery_ShouldAppendAac_WhenAudioUrlMissingCodec()
    {
        var url = "https://host/api/indexed-files/1/hls-stream/audio/1/segments/init.m4s?streamSessionId=abc";

        var result = HlsStreamUrlHelper.EnsureWindowsHlsAudioTranscodeQuery(url);

        result.Should().Contain("TranscodingAudioCodec=aac");
        result.Should().StartWith(url);
    }

    [Test]
    public void EnsureWindowsHlsAudioTranscodeQuery_ShouldLeaveUrl_WhenAlreadyHasCodec()
    {
        var url = "https://host/api/indexed-files/1/hls-stream/audio/1/index.m3u8?TranscodingAudioCodec=aac";

        HlsStreamUrlHelper.EnsureWindowsHlsAudioTranscodeQuery(url).Should().Be(url);
    }

    [Test]
    public void EnsureWindowsHlsAudioTranscodeQuery_ShouldLeaveVideoUrlUnchanged()
    {
        var url = "https://host/api/indexed-files/1/hls-stream/video/720p/segments/0.m4s";

        HlsStreamUrlHelper.EnsureWindowsHlsAudioTranscodeQuery(url).Should().Be(url);
    }

    [Test]
    public void AbsolutizeManifestUrls_ShouldInjectAacOnMasterAudioMediaUri()
    {
        var manifest = """
            #EXTM3U
            #EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="audio",NAME="FR",DEFAULT=YES,URI="audio/1/index.m3u8?streamSessionId=abc"
            #EXT-X-STREAM-INF:BANDWIDTH=2000000,AUDIO="audio"
            video/720p/index.m3u8?streamSessionId=abc
            """;
        var baseUrl = new Uri("https://host/api/indexed-files/1/hls-stream/manifest.m3u8?streamSessionId=abc");

        var result = HlsStreamUrlHelper.AbsolutizeManifestUrls(manifest, baseUrl);

        result.Should().Contain("URI=\"https://host/api/indexed-files/1/hls-stream/audio/1/index.m3u8?streamSessionId=abc&TranscodingAudioCodec=aac\"");
        result.Should().Contain("https://host/api/indexed-files/1/hls-stream/video/720p/index.m3u8?streamSessionId=abc");
        result.Should().NotContain("video/720p/index.m3u8?streamSessionId=abc&TranscodingAudioCodec");
    }

    [Test]
    public void AbsolutizeManifestUrls_ShouldInjectAacOnAudioPlaylistSegments()
    {
        var manifest = """
            #EXTM3U
            #EXT-X-MAP:URI="segments/init.m4s?streamSessionId=abc"
            #EXTINF:4.0,
            segments/191.m4s?streamSessionId=abc
            """;
        var baseUrl = new Uri(
            "https://host/api/indexed-files/1/hls-stream/audio/1/index.m3u8?streamSessionId=abc");

        var result = HlsStreamUrlHelper.AbsolutizeManifestUrls(manifest, baseUrl);

        result.Should().Contain(
            "URI=\"https://host/api/indexed-files/1/hls-stream/audio/1/segments/init.m4s?streamSessionId=abc&TranscodingAudioCodec=aac\"");
        result.Should().Contain(
            "https://host/api/indexed-files/1/hls-stream/audio/1/segments/191.m4s?streamSessionId=abc&TranscodingAudioCodec=aac");
    }
}
