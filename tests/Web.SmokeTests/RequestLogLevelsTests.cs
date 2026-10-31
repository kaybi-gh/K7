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
}
