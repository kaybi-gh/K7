using System.Security.Claims;
using K7.Server.Application.Features.LinkPreview.Queries.GetLinkPreview;
using K7.Server.Web.Infrastructure;
using K7.Server.Web.Middleware;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Http;

namespace K7.Server.Web.SmokeTests;

[TestFixture]
public class LinkPreviewDocumentTests
{
    [Test]
    public void Render_ShouldUseMediaTitleAndPoster_WhenPreviewHasPicture()
    {
        var pictureId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var html = LinkPreviewDocument.Render(
            "https://k7.example.com",
            "/movies/" + Guid.NewGuid(),
            "K7",
            "Fallback description",
            LinkPreviewUrls.ImageAsset,
            new LinkPreview("Sintel", "A girl and a dragon.", pictureId));

        html.Should().Contain("property=\"og:title\" content=\"Sintel\"");
        html.Should().Contain("property=\"og:description\" content=\"A girl and a dragon.\"");
        html.Should().Contain(
            "property=\"og:image\" content=\"https://k7.example.com/api/metadata-pictures/dddddddd-dddd-dddd-dddd-dddddddddddd?size=Medium\"");
        html.Should().Contain("name=\"twitter:card\" content=\"summary_large_image\"");
        html.Should().NotContain(LinkPreviewUrls.ImageAsset);
    }

    [Test]
    public void Render_ShouldFallBackToK7Image_WhenPreviewHasNoPicture()
    {
        var html = LinkPreviewDocument.Render(
            "https://k7.example.com",
            "/movies/" + Guid.NewGuid(),
            "K7",
            "Fallback description",
            LinkPreviewUrls.ImageAsset,
            new LinkPreview("Sintel", null, null));

        html.Should().Contain("property=\"og:title\" content=\"Sintel\"");
        html.Should().Contain(
            $"property=\"og:image\" content=\"https://k7.example.com/{LinkPreviewUrls.ImageAsset}\"");
        html.Should().Contain("name=\"twitter:card\" content=\"summary_large_image\"");
        html.Should().Contain($"property=\"og:image:width\" content=\"{LinkPreviewUrls.ImageWidth}\"");
    }

    [Test]
    public void ResolveTitle_ShouldJoinServerNameAndDescription_WhenNoPreview()
    {
        LinkPreviewDocument.ResolveTitle("K7", "Self-hosted media server for movies, TV shows, and music.", null)
            .Should().Be("K7 - Self-hosted media server for movies, TV shows, and music.");
    }

    [Test]
    public void Render_ShouldHtmlEncodeTitle()
    {
        var html = LinkPreviewDocument.Render(
            "https://k7.example.com",
            "/movies/" + Guid.NewGuid(),
            "K7",
            "Fallback",
            LinkPreviewUrls.ImageAsset,
            new LinkPreview("<script>alert(1)</script>", "A & B", null));

        html.Should().Contain("content=\"&lt;script&gt;alert(1)&lt;/script&gt;\"");
        html.Should().Contain("content=\"A &amp; B\"");
        html.Should().NotContain("<script>alert(1)</script>");
    }

    [Test]
    public void ShouldHandle_ShouldBeTrue_WhenAnonymousMediaPageGet()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/movies/" + Guid.NewGuid();
        context.User = new ClaimsPrincipal(new ClaimsIdentity());

        LinkPreviewDocumentMiddleware.ShouldHandle(context).Should().BeTrue();
    }

    [Test]
    public void ShouldHandle_ShouldBeTrue_WhenAnonymousHomeGet()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/";
        context.User = new ClaimsPrincipal(new ClaimsIdentity());

        LinkPreviewDocumentMiddleware.ShouldHandle(context).Should().BeTrue();
    }

    [Test]
    public void ShouldHandle_ShouldBeTrue_WhenAnonymousEpisodeGet()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/series/019fc962-182a-7399-ae88-34e8a26ab7e3/seasons/1/episodes/1";
        context.User = new ClaimsPrincipal(new ClaimsIdentity());

        LinkPreviewDocumentMiddleware.ShouldHandle(context).Should().BeTrue();
    }

    [Test]
    public void ShouldHandle_ShouldBeFalse_WhenUserIsAuthenticated()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/movies/" + Guid.NewGuid();
        context.User = new ClaimsPrincipal(new ClaimsIdentity("cookies"));

        LinkPreviewDocumentMiddleware.ShouldHandle(context).Should().BeFalse();
    }

    [Test]
    public void ShouldHandle_ShouldBeFalse_WhenPathIsNotAMediaPage()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/welcome";
        context.User = new ClaimsPrincipal(new ClaimsIdentity());

        LinkPreviewDocumentMiddleware.ShouldHandle(context).Should().BeFalse();
    }

    [Test]
    public void ShouldIncludeMediaPreview_ShouldBeTrue_WhenEnabledAndMediaPath()
    {
        LinkPreviewDocumentMiddleware
            .ShouldIncludeMediaPreview("/movies/" + Guid.NewGuid(), true)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldIncludeMediaPreview_ShouldBeFalse_WhenDisabled()
    {
        LinkPreviewDocumentMiddleware
            .ShouldIncludeMediaPreview("/movies/" + Guid.NewGuid(), false)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldIncludeMediaPreview_ShouldBeFalse_WhenHomePath()
    {
        LinkPreviewDocumentMiddleware.ShouldIncludeMediaPreview("/", true).Should().BeFalse();
    }

    [Test]
    public void ServerFeatureFlagsDto_ShouldEnableMediaLinkPreviews_ByDefault()
    {
        new ServerFeatureFlagsDto().MediaLinkPreviewsEnabled.Should().BeTrue();
    }
}
