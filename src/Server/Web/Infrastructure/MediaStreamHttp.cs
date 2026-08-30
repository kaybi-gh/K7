using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;

namespace K7.Server.Web.Infrastructure;

/// <summary>
/// Progressive /direct-stream responses. Players pause reading once their buffer is full;
/// Kestrel's default min response data rate then aborts the connection and the picture
/// freezes while audio may keep playing from what is already decoded.
/// </summary>
public static class MediaStreamHttp
{
    public static IResult File(string path, string contentType, string? fileDownloadName = null) =>
        new PreparedMediaFileResult(path, contentType, fileDownloadName);

    public static void PrepareLongRunningResponse(HttpContext http)
    {
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var minRate = http.Features.Get<IHttpMinResponseDataRateFeature>();
        if (minRate is not null)
            minRate.MinDataRate = null;

        http.Response.Headers.CacheControl = "private, no-transform";
        http.Response.Headers.AcceptRanges = "bytes";
        http.Response.Headers["X-Accel-Buffering"] = "no";
    }

    private sealed class PreparedMediaFileResult(
        string path,
        string contentType,
        string? fileDownloadName) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            PrepareLongRunningResponse(httpContext);
            await Results.File(
                    path,
                    contentType: contentType,
                    enableRangeProcessing: true,
                    fileDownloadName: fileDownloadName)
                .ExecuteAsync(httpContext);
        }
    }
}
