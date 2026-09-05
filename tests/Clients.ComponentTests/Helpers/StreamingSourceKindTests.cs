using AwesomeAssertions;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class StreamingSourceKindTests
{
    [Test]
    public void IsHls_ShouldBeTrue_WhenMimeOrUrlIsPlaylist()
    {
        StreamingSourceKind.IsHls("application/vnd.apple.mpegurl", "https://host/file.mkv")
            .Should().BeTrue();
        StreamingSourceKind.IsHls("video/mp4", "https://host/api/indexed-files/x/hls-stream/manifest.m3u8")
            .Should().BeTrue();
    }

    [Test]
    public void IsHls_ShouldBeFalse_WhenDirectStreamOrLocalFile()
    {
        StreamingSourceKind.IsHls("video/x-matroska", "https://host/api/indexed-files/x/direct-stream")
            .Should().BeFalse();
        StreamingSourceKind.IsHls("video/x-matroska", "/data/user/0/com.k7/files/a.mkv")
            .Should().BeFalse();
    }

    [Test]
    public void TryBuildHlsManifestUrl_ShouldReplaceDirectStreamPath()
    {
        StreamingSourceKind.TryBuildHlsManifestUrl(
                "https://host/api/indexed-files/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/direct-stream",
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                out var hls)
            .Should().BeTrue();
        hls.Should().Be(
            "https://host/api/indexed-files/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/hls-stream/manifest.m3u8?StreamSessionId=bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    }

    [Test]
    public void TryBuildHlsManifestUrl_ShouldAppendVideoCodecsOnly_WhenVideoJsCompatible()
    {
        StreamingSourceKind.TryBuildHlsManifestUrl(
                "https://host/api/indexed-files/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/direct-stream",
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                out var hls,
                videoJsCompatible: true)
            .Should().BeTrue();
        hls.Should().Contain("VideoCodecsOnly=true");
        hls.Should().Contain("StreamSessionId=bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    }

    [Test]
    public void EnsureVideoJsHlsManifestQuery_ShouldBeIdempotent()
    {
        var once = StreamingSourceKind.EnsureVideoJsHlsManifestQuery(
            "https://host/hls-stream/manifest.m3u8?StreamSessionId=1");
        var twice = StreamingSourceKind.EnsureVideoJsHlsManifestQuery(once);

        once.Should().Be(twice);
        once.Should().Contain("VideoCodecsOnly=true");
    }

    [Test]
    public void TryBuildDirectStreamUrl_ShouldReplaceHlsManifestPath()
    {
        StreamingSourceKind.TryBuildDirectStreamUrl(
                "https://host/api/indexed-files/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/hls-stream/manifest.m3u8?StreamSessionId=bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb&Quality=720p",
                out var direct)
            .Should().BeTrue();
        direct.Should().Be(
            "https://host/api/indexed-files/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/direct-stream");
    }
}
