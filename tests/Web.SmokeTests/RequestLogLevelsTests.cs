using K7.Server.Web.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace K7.Server.Web.SmokeTests;

[TestFixture]
public class RequestLogLevelsTests
{
    [TestCase("/api/indexed-files/3f2b8c1e-0000-0000-0000-000000000000/hls-stream/video/12.m4s")]
    [TestCase("/api/indexed-files/3f2b8c1e-0000-0000-0000-000000000000/direct-stream")]
    [TestCase("/api/stream-sessions/3f2b8c1e-0000-0000-0000-000000000000/hls/main.m3u8")]
    [TestCase("/api/metadata-pictures/3f2b8c1e-0000-0000-0000-000000000000")]
    [TestCase("/rest/getCoverArt.view")]
    [TestCase("/hub/negotiate")]
    [TestCase("/api/indexed-files/3f2b8c1e-0000-0000-0000-000000000000/subtitles/7.vtt")]
    public void IsHighVolumePath_ShouldReturnTrue_WhenStreamingImageOrHubPath(string path)
    {
        RequestLogLevels.IsHighVolumePath(new PathString(path)).Should().BeTrue();
    }

    [TestCase("/api/home/feed")]
    [TestCase("/api/medias/3f2b8c1e-0000-0000-0000-000000000000")]
    [TestCase("/connect/token")]
    [TestCase("/hubs-are-not-the-hub")]
    public void IsHighVolumePath_ShouldReturnFalse_WhenRegularApiPath(string path)
    {
        RequestLogLevels.IsHighVolumePath(new PathString(path)).Should().BeFalse();
    }

    [TestCase("/api/indexed-files/3f2b8c1e-0000-0000-0000-000000000000/subtitles/7.vtt")]
    [TestCase("/api/indexed-files/3f2b8c1e-0000-0000-0000-000000000000/hls-stream/subtitles/7/segments/4.vtt")]
    public void IsSubtitleExtractionInProgress_ShouldReturnTrue_WhenVttAnswers503(string path)
    {
        RequestLogLevels.IsSubtitleExtractionInProgress(new PathString(path), 503).Should().BeTrue();
    }

    [TestCase("/api/indexed-files/3f2b8c1e-0000-0000-0000-000000000000/subtitles/7.vtt", 500)]
    [TestCase("/api/indexed-files/3f2b8c1e-0000-0000-0000-000000000000/subtitles/7.vtt", 200)]
    [TestCase("/api/indexed-files/3f2b8c1e-0000-0000-0000-000000000000/hls-stream/video/12.m4s", 503)]
    [TestCase("/api/home/feed", 503)]
    public void IsSubtitleExtractionInProgress_ShouldReturnFalse_WhenNotAVtt503(string path, int statusCode)
    {
        RequestLogLevels.IsSubtitleExtractionInProgress(new PathString(path), statusCode).Should().BeFalse();
    }
}
