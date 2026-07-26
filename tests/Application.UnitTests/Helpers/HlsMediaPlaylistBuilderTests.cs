using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class HlsMediaPlaylistBuilderTests
{
    [Test]
    public void Build_ShouldShareExtinfTimeline_ForAudioAndVideo()
    {
        double[] durations = [2.0, 1.5, 2.5];
        var sessionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var videoQs = HlsMediaPlaylistBuilder.BuildQueryString(sessionId, ("TranscodingVideoCodec", null));
        var audioQs = HlsMediaPlaylistBuilder.BuildQueryString(sessionId, ("TranscodingAudioCodec", null));

        var video = HlsMediaPlaylistBuilder.Build(durations, videoQs, i => $"video/segments/{i}.m4s");
        var audio = HlsMediaPlaylistBuilder.Build(durations, audioQs, i => $"audio/segments/{i}.m4s");

        ExtractExtinfLines(video).Should().Equal(ExtractExtinfLines(audio));
        video.Should().Contain("#EXT-X-TARGETDURATION:3");
        audio.Should().Contain("#EXT-X-TARGETDURATION:3");
        video.Should().Contain("#EXT-X-MAP:URI=\"segments/init.m4s?streamSessionId=");
        audio.Should().Contain("audio/segments/1.m4s?streamSessionId=");
    }

    [Test]
    public void Build_ShouldSnapExtXStart_ToPreviousBoundary()
    {
        double[] durations = [2.0, 4.0, 2.0];
        var playlist = HlsMediaPlaylistBuilder.Build(
            durations,
            "?streamSessionId=1",
            i => $"{i}.m4s",
            startSeconds: 5.5);

        playlist.Should().Contain("#EXT-X-START:TIME-OFFSET=2.000,PRECISE=NO");
    }

    [Test]
    public void BuildQueryString_ShouldOmitEmptyOptionalParams()
    {
        var qs = HlsMediaPlaylistBuilder.BuildQueryString(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ("TranscodingVideoCodec", "h264"),
            ("SubtitleBurnInStreamIndex", null));

        qs.Should().Be("?streamSessionId=11111111-1111-1111-1111-111111111111&TranscodingVideoCodec=h264");
        qs.Should().NotContain("SubtitleBurnInStreamIndex");
    }

    private static string[] ExtractExtinfLines(string playlist) =>
        playlist.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.StartsWith("#EXTINF:", StringComparison.Ordinal))
            .ToArray();
}
