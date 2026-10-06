using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

public class HlsManifestUrlTests
{
    [Test]
    public void WithAudioTrackTranscodings_ShouldAppendQueryParam()
    {
        var url = "https://host/api/indexed-files/g/hls-stream/manifest.m3u8?StreamSessionId=s";
        var next = HlsManifestUrl.WithAudioTrackTranscodings(url, new Dictionary<int, string> { [1] = "aac" });

        next.Should().Contain("AudioTrackTranscodings=1%3Aaac");
        next.Should().Contain("StreamSessionId=s");
    }

    [Test]
    public void WithAudioTrackTranscodings_ShouldReplaceExistingParam_WhenItIsFirst()
    {
        var url = "https://host/manifest.m3u8?AudioTrackTranscodings=0%3Aaac&StreamSessionId=s";
        var next = HlsManifestUrl.WithAudioTrackTranscodings(url, new Dictionary<int, string> { [1] = "aac" });

        next.Should().StartWith("https://host/manifest.m3u8?");
        next.Should().Contain("StreamSessionId=s");
        next.Should().Contain("AudioTrackTranscodings=1%3Aaac");
        next.Should().NotContain("0%3Aaac");
    }
}
