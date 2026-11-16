using System.Net;
using System.Text;
using K7.Clients.Shared.Services;

namespace K7.Clients.Web.Services;

/// <summary>
/// While a public share page is open, the regular players still ask the signed-in API for
/// things a visitor has no right to (preferences, intro markers, next episode, history).
/// Those calls are answered locally with an empty result: they never reach the server and
/// never trigger the sign-in redirect. Only what a share serves goes through.
/// </summary>
public sealed class ShareVisitorHttpHandler(ISharePlaybackScope scope) : DelegatingHandler
{
    private static readonly string[] AllowedPaths =
    [
        "/api/public/shares/",
        "/api/metadata-pictures/",
        "/api/indexed-files/"
    ];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!scope.IsVisitorPage || IsAllowed(request.RequestUri))
            return base.SendAsync(request, cancellationToken);

        // GET: a JSON null, which callers already treat as "nothing set". Writes: accepted, ignored.
        var response = request.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            }
            : new HttpResponseMessage(HttpStatusCode.NoContent);

        response.RequestMessage = request;
        return Task.FromResult(response);
    }

    internal static bool IsAllowed(Uri? uri)
    {
        if (uri is null)
            return false;

        var path = uri.IsAbsoluteUri ? uri.AbsolutePath : "/" + uri.OriginalString.TrimStart('/');
        foreach (var allowed in AllowedPaths)
        {
            if (path.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
