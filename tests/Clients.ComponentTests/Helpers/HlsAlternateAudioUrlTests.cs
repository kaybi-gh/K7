using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class HlsAlternateAudioUrlTests
{
    [Test]
    public void TryBuildSlaveUrl_ShouldPointAtAudioPlaylistOnProxy()
    {
        var slave = HlsAlternateAudioUrl.TryBuildSlaveUrl(
            "http://127.0.0.1:41000/hls/manifest.m3u8",
            "https://host/api/indexed-files/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/hls-stream/manifest.m3u8?StreamSessionId=bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb&Quality=1080p",
            audioTrackIndex: 2,
            startSeconds: 90.5);

        slave.Should().Be(
            "http://127.0.0.1:41000/hls/audio/2/index.m3u8?streamSessionId=bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb&startSeconds=90.500");
    }

    [Test]
    public void TryBuildSlaveUrl_ShouldReturnNull_WhenSessionIsMissing()
    {
        HlsAlternateAudioUrl.TryBuildSlaveUrl(
                "http://127.0.0.1:41000/hls/manifest.m3u8",
                "https://host/api/indexed-files/x/hls-stream/manifest.m3u8",
                audioTrackIndex: 0,
                startSeconds: 10)
            .Should().BeNull();
    }
}
