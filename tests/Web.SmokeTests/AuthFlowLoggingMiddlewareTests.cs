using K7.Server.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace K7.Server.Web.SmokeTests;

[TestFixture]
public class AuthFlowLoggingMiddlewareTests
{
    [Test]
    public void IsAuthPath_ShouldCoverNativeLoginSurface()
    {
        AuthFlowLoggingMiddleware.IsAuthPath("/sign-in").Should().BeTrue();
        AuthFlowLoggingMiddleware.IsAuthPath("/sign-in/two-factor").Should().BeTrue();
        AuthFlowLoggingMiddleware.IsAuthPath("/welcome").Should().BeTrue();
        AuthFlowLoggingMiddleware.IsAuthPath("/connect/authorize").Should().BeTrue();
        AuthFlowLoggingMiddleware.IsAuthPath("/connect/token").Should().BeTrue();
        AuthFlowLoggingMiddleware.IsAuthPath("/auth/complete").Should().BeTrue();
        AuthFlowLoggingMiddleware.IsAuthPath("/api/users/me").Should().BeFalse();
        AuthFlowLoggingMiddleware.IsAuthPath("/library").Should().BeFalse();
    }

    [Test]
    public void ReadReturnUrl_ShouldPreferFormOverQuery()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?ReturnUrl=/welcome");
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["ReturnUrl"] = "/connect/authorize?client_id=k7-native"
        });

        AuthFlowLoggingMiddleware.ReadReturnUrl(context.Request)
            .Should().StartWith("/connect/authorize");
    }
}
