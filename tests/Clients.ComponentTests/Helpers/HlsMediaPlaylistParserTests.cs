using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class HlsMediaPlaylistParserTests
{
    private const string PlaylistUrl =
        "https://host/api/indexed-files/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/hls-stream/audio/1/index.m3u8?streamSessionId=bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

    [Test]
    public void TryParse_ShouldResolveMapAndSegments()
    {
        var text = """
            #EXTM3U
            #EXT-X-PLAYLIST-TYPE:VOD
            #EXT-X-TARGETDURATION:6
            #EXT-X-VERSION:7
            #EXT-X-MAP:URI="segments/init.m4s?streamSessionId=bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
            #EXTINF:6.000000,
            segments/0.m4s?streamSessionId=bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb
            #EXTINF:6.000000,
            segments/1.m4s?streamSessionId=bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb
            #EXT-X-ENDLIST
            """;

        var ok = HlsMediaPlaylistParser.TryParse(text, PlaylistUrl, out var mapUrl, out var segments);

        ok.Should().BeTrue();
        mapUrl.Should().EndWith("/hls-stream/audio/1/segments/init.m4s?streamSessionId=bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        segments.Should().HaveCount(2);
        segments[0].DurationSeconds.Should().Be(6);
        segments[0].Url.Should().Contain("/segments/0.m4s");
        segments[1].Url.Should().Contain("/segments/1.m4s");
    }

    [Test]
    public void FirstSegmentIndexAtOrBefore_ShouldAlignToContainingSegment()
    {
        var segments = new HlsMediaPlaylistParser.Segment[]
        {
            new(6, "0"),
            new(6, "1"),
            new(6, "2")
        };

        HlsMediaPlaylistParser.FirstSegmentIndexAtOrBefore(segments, 0).Should().Be(0);
        HlsMediaPlaylistParser.FirstSegmentIndexAtOrBefore(segments, 5.9).Should().Be(0);
        HlsMediaPlaylistParser.FirstSegmentIndexAtOrBefore(segments, 6).Should().Be(1);
        HlsMediaPlaylistParser.FirstSegmentIndexAtOrBefore(segments, 13).Should().Be(2);
        HlsMediaPlaylistParser.FirstSegmentIndexAtOrBefore(segments, 90).Should().Be(2);
    }
}
