using K7.Server.Web.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace K7.Server.Application.FunctionalTests.Infrastructure;

[TestFixture]
public class MediaStreamHttpTests
{
    [Test]
    public void PrepareLongRunningResponse_ShouldDisableBufferingAndMinDataRate()
    {
        var http = new DefaultHttpContext();

        MediaStreamHttp.PrepareLongRunningResponse(http);

        http.Response.Headers.CacheControl.ToString().Should().Contain("no-transform");
        http.Response.Headers.AcceptRanges.ToString().Should().Be("bytes");
        http.Response.Headers["X-Accel-Buffering"].ToString().Should().Be("no");
    }
}
