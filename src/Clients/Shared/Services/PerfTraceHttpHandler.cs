using System.Diagnostics;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Logs each API call through <see cref="PerfTrace"/> when measurements are on. The duration
/// runs until the response headers (or the full body when the caller buffers it); compressed
/// responses report an unknown size because decompression drops Content-Length.
/// Outermost handler, so it includes the time spent in the authentication handler.
/// </summary>
public sealed class PerfTraceHttpHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        PerfTrace.IsEnabled
            ? SendTracedAsync(request, cancellationToken)
            : base.SendAsync(request, cancellationToken);

    private async Task<HttpResponseMessage> SendTracedAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            PerfTrace.Request(
                request.Method.Method,
                request.RequestUri?.PathAndQuery,
                (int)response.StatusCode,
                Stopwatch.GetElapsedTime(start),
                response.Content.Headers.ContentLength);
            return response;
        }
        catch
        {
            PerfTrace.Request(request.Method.Method, request.RequestUri?.PathAndQuery, null, Stopwatch.GetElapsedTime(start), null);
            throw;
        }
    }
}
