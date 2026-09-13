using System.Net;
using System.Text;
using K7.Server.Application.Features.LinkPreview.Queries.GetLinkPreview;

namespace K7.Server.Web.Infrastructure;

public static class LinkPreviewDocument
{
    public static string ResolveTitle(string serverName, string? fallbackDescription, LinkPreview? preview)
    {
        if (!string.IsNullOrWhiteSpace(preview?.Title))
            return preview.Title;

        if (!string.IsNullOrWhiteSpace(fallbackDescription))
            return $"{serverName} - {fallbackDescription}";

        return serverName;
    }

    public static string Render(
        string origin,
        PathString path,
        string serverName,
        string fallbackDescription,
        string fallbackImagePath,
        LinkPreview? preview)
    {
        var title = ResolveTitle(serverName, fallbackDescription, preview);
        var description = !string.IsNullOrWhiteSpace(preview?.Description)
            ? preview.Description
            : fallbackDescription;
        var imageUrl = LinkPreviewUrls.Image(origin, preview?.PictureId, fallbackImagePath);
        var canonicalUrl = LinkPreviewUrls.CanonicalPage(origin, path);
        var encodedTitle = WebUtility.HtmlEncode(title);
        var encodedDescription = WebUtility.HtmlEncode(description);
        var encodedImage = WebUtility.HtmlEncode(imageUrl);
        var encodedCanonical = WebUtility.HtmlEncode(canonicalUrl);
        var encodedServer = WebUtility.HtmlEncode(serverName);
        var useDefaultImage = preview?.PictureId is null;

        var html = new StringBuilder(2048);
        html.Append("<!DOCTYPE html><html><head>");
        html.Append("<meta charset=\"utf-8\" />");
        html.Append("<meta name=\"robots\" content=\"noindex, nofollow\" />");
        html.Append("<title>").Append(encodedTitle).Append("</title>");
        html.Append("<meta name=\"description\" content=\"").Append(encodedDescription).Append("\" />");
        html.Append("<meta property=\"og:type\" content=\"website\" />");
        html.Append("<meta property=\"og:site_name\" content=\"").Append(encodedServer).Append("\" />");
        html.Append("<meta property=\"og:title\" content=\"").Append(encodedTitle).Append("\" />");
        html.Append("<meta property=\"og:description\" content=\"").Append(encodedDescription).Append("\" />");
        html.Append("<meta property=\"og:url\" content=\"").Append(encodedCanonical).Append("\" />");
        html.Append("<meta property=\"og:image\" content=\"").Append(encodedImage).Append("\" />");
        if (useDefaultImage)
        {
            html.Append("<meta property=\"og:image:type\" content=\"image/png\" />");
            html.Append("<meta property=\"og:image:width\" content=\"").Append(LinkPreviewUrls.ImageWidth).Append("\" />");
            html.Append("<meta property=\"og:image:height\" content=\"").Append(LinkPreviewUrls.ImageHeight).Append("\" />");
        }

        html.Append("<meta property=\"og:image:alt\" content=\"").Append(encodedTitle).Append("\" />");
        html.Append("<meta name=\"twitter:card\" content=\"summary_large_image\" />");
        html.Append("<meta name=\"twitter:title\" content=\"").Append(encodedTitle).Append("\" />");
        html.Append("<meta name=\"twitter:description\" content=\"").Append(encodedDescription).Append("\" />");
        html.Append("<meta name=\"twitter:image\" content=\"").Append(encodedImage).Append("\" />");
        html.Append("<script>location.replace('/welcome?returnUrl='+encodeURIComponent(location.pathname+location.search+location.hash))</script>");
        html.Append("</head><body></body></html>");
        return html.ToString();
    }
}
