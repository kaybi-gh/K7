using K7.Server.Web.Middleware;
using Microsoft.AspNetCore.Http;

namespace K7.Server.Web.SmokeTests;

[TestFixture]
public class AuthFormBadRequestRecoveryMiddlewareTests
{
    [Test]
    public void ShouldRecover_ShouldBeTrue_WhenEmpty400OnSignInPost()
    {
        var context = CreateContext("/sign-in", "POST", StatusCodes.Status400BadRequest);

        AuthFormBadRequestRecoveryMiddleware.ShouldRecover(context).Should().BeTrue();
    }

    [Test]
    public void ShouldRecover_ShouldBeFalse_WhenJsonProblemDetails()
    {
        var context = CreateContext("/sign-in", "POST", StatusCodes.Status400BadRequest);
        context.Response.ContentType = "application/problem+json";

        AuthFormBadRequestRecoveryMiddleware.ShouldRecover(context).Should().BeFalse();
    }

    [Test]
    public void ShouldRecover_ShouldBeFalse_WhenGetOrApi()
    {
        AuthFormBadRequestRecoveryMiddleware.ShouldRecover(
            CreateContext("/sign-in", "GET", StatusCodes.Status400BadRequest)).Should().BeFalse();
        AuthFormBadRequestRecoveryMiddleware.ShouldRecover(
            CreateContext("/api/users/me", "POST", StatusCodes.Status400BadRequest)).Should().BeFalse();
        AuthFormBadRequestRecoveryMiddleware.ShouldRecover(
            CreateContext("/sign-in", "POST", StatusCodes.Status302Found)).Should().BeFalse();
    }

    [Test]
    public void IsAuthFormPath_ShouldMatchIdentityPages()
    {
        AuthFormBadRequestRecoveryMiddleware.IsAuthFormPath("/sign-in").Should().BeTrue();
        AuthFormBadRequestRecoveryMiddleware.IsAuthFormPath("/sign-in/two-factor").Should().BeTrue();
        AuthFormBadRequestRecoveryMiddleware.IsAuthFormPath("/sign-up").Should().BeTrue();
        AuthFormBadRequestRecoveryMiddleware.IsAuthFormPath("/account/logout").Should().BeTrue();
        AuthFormBadRequestRecoveryMiddleware.IsAuthFormPath("/connect/authorize").Should().BeFalse();
    }

    private static DefaultHttpContext CreateContext(string path, string method, int statusCode)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.StatusCode = statusCode;
        return context;
    }
}
