using K7.Server.Web.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Web.SmokeTests;

[TestFixture]
public class ResponseCompressionExtensionsTests
{
    [TestCase("GET", "/api/home/feed")]
    [TestCase("GET", "/api/medias/3f2b8c1e-0000-0000-0000-000000000000")]
    [TestCase("QUERY", "/api/medias")]
    public void ShouldCompress_ShouldAccept_WhenApiRead(string method, string path)
    {
        ResponseCompressionExtensions.ShouldCompress(CreateContext(method, path)).Should().BeTrue();
    }

    [TestCase("POST", "/api/medias/progress")]
    [TestCase("GET", "/api/admin/api-keys")]
    [TestCase("GET", "/api/users/me/client-app-passwords")]
    [TestCase("GET", "/api/authentication/login")]
    [TestCase("GET", "/connect/userinfo")]
    [TestCase("GET", "/_framework/blazor.web.js")]
    public void ShouldCompress_ShouldReject_WhenWriteSecretOrNonApi(string method, string path)
    {
        ResponseCompressionExtensions.ShouldCompress(CreateContext(method, path)).Should().BeFalse();
    }

    [Test]
    public async Task UseK7ResponseCompression_ShouldBrotliEncodeJson_WhenClientAcceptsIt()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddK7ResponseCompression()
            .BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        app.UseK7ResponseCompression();
        app.Run(async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(new string('a', 4096));
        });

        var context = CreateContext("GET", "/api/home/feed");
        context.RequestServices = services;
        context.Request.Headers.AcceptEncoding = "br, gzip";
        var body = new MemoryStream();
        context.Response.Body = body;

        await app.Build()(context);

        context.Response.Headers.ContentEncoding.ToString().Should().Be("br");
        body.Length.Should().BeLessThan(4096);
    }

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        return context;
    }
}
