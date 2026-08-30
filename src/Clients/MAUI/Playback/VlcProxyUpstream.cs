using System.Globalization;

namespace K7.Clients.MAUI.Playback;

/// <summary>
/// Upstream GET/HEAD for the LibVLC loopback proxy. The server already holds
/// HLS segment requests (90s init / 180s media) before 503. VLC does not retry
/// 503, so a few extra attempts here cover ffmpeg finishing just after that wait.
/// </summary>
internal static class VlcProxyUpstream
{
    public const int ServiceUnavailableRetries = 4;

    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient upstream,
        string method,
        string upstreamUrl,
        string? range,
        CancellationToken cancellationToken,
        Func<int, CancellationToken, Task>? delayAsync = null)
    {
        HttpResponseMessage? response = null;
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(
                method.Equals("HEAD", StringComparison.OrdinalIgnoreCase)
                    ? HttpMethod.Head
                    : HttpMethod.Get,
                upstreamUrl);
            if (!string.IsNullOrEmpty(range))
                request.Headers.TryAddWithoutValidation("Range", range);

            response = await upstream
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if ((int)response.StatusCode != 503 || attempt >= ServiceUnavailableRetries)
                return response;

            VlcPlayerLog.Warn(
                "vlc-proxy 503 retry "
                + (attempt + 1).ToString(CultureInfo.InvariantCulture)
                + "/"
                + ServiceUnavailableRetries.ToString(CultureInfo.InvariantCulture)
                + " "
                + VlcPlayerLog.SummarizeUrl(upstreamUrl));

            response.Dispose();
            var delay = delayAsync ?? DefaultDelayAsync;
            await delay(attempt, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task DefaultDelayAsync(int attempt, CancellationToken cancellationToken) =>
        Task.Delay(1000 * (attempt + 1), cancellationToken);
}
