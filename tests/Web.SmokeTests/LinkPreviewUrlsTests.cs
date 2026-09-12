using K7.Server.Web.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace K7.Server.Web.SmokeTests;

[TestFixture]
public class LinkPreviewUrlsTests
{
    [Test]
    public void Origin_ShouldUseRequestHost_WhenPresent()
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = "https";
        request.Host = new HostString("k7.example.com");

        LinkPreviewUrls.Origin(request, "https://fallback.example").Should().Be("https://k7.example.com");
    }

    [Test]
    public void Origin_ShouldTrimTrailingSlash()
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = "https";
        request.Host = new HostString("k7.example.com:7443");

        LinkPreviewUrls.Origin(request, "https://fallback.example/").Should().Be("https://k7.example.com:7443");
    }

    [Test]
    public void Origin_ShouldUseFallback_WhenHostMissing()
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = "https";

        LinkPreviewUrls.Origin(request, "https://fallback.example/").Should().Be("https://fallback.example");
    }

    [Test]
    public void Combine_ShouldKeepAbsolutePath()
    {
        LinkPreviewUrls.Combine("https://k7.example.com", "https://cdn.example/og.png")
            .Should().Be("https://cdn.example/og.png");
    }

    [Test]
    public void Combine_ShouldPrefixRelativePath()
    {
        LinkPreviewUrls.Combine("https://k7.example.com", "_content/k7/og-image.png")
            .Should().Be("https://k7.example.com/_content/k7/og-image.png");
        LinkPreviewUrls.Combine("https://k7.example.com", "/icon.png")
            .Should().Be("https://k7.example.com/icon.png");
    }

    [Test]
    public void MetadataPicturePath_ShouldPointAtAnonymousPictureEndpoint()
    {
        var id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        LinkPreviewUrls.MetadataPicturePath(id)
            .Should().Be("/api/metadata-pictures/dddddddd-dddd-dddd-dddd-dddddddddddd?size=Medium");
    }

    [Test]
    public void CanonicalPage_ShouldDefaultToRoot()
    {
        LinkPreviewUrls.CanonicalPage("https://k7.example.com", new PathString())
            .Should().Be("https://k7.example.com/");
        LinkPreviewUrls.CanonicalPage("https://k7.example.com", "/movies/42")
            .Should().Be("https://k7.example.com/movies/42");
    }
}
