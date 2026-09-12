using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

namespace K7.Server.Application.Features.Scrobbling.Services;

/// <summary>
/// ListenBrainz (and similar) advertise limits via Retry-After / X-RateLimit-Reset-In.
/// Cap waits so a sticky 429 cannot stall the scrobble queue for minutes.
/// </summary>
public static class ScrobbleRateLimit
{
    public static TimeSpan? TryGetDelay(HttpResponseMessage response, TimeSpan maxDelay)
    {
        if (response.StatusCode != HttpStatusCode.TooManyRequests
            && (int)response.StatusCode != 429)
        {
            return null;
        }

        if (response.Headers.RetryAfter is { } retryAfter)
        {
            if (retryAfter.Delta is { } delta && delta > TimeSpan.Zero)
                return Clamp(delta, maxDelay);

            if (retryAfter.Date is { } date)
            {
                var until = date - DateTimeOffset.UtcNow;
                if (until > TimeSpan.Zero)
                    return Clamp(until, maxDelay);
            }
        }

        if (TryReadHeaderSeconds(response.Headers, "X-RateLimit-Reset-In", out var resetIn)
            && resetIn > 0)
        {
            return Clamp(TimeSpan.FromSeconds(resetIn), maxDelay);
        }

        return TimeSpan.FromSeconds(5);
    }

    private static bool TryReadHeaderSeconds(
        HttpResponseHeaders headers,
        string name,
        out double seconds)
    {
        seconds = 0;
        if (!headers.TryGetValues(name, out var values))
            return false;

        var raw = values.FirstOrDefault();
        return !string.IsNullOrWhiteSpace(raw)
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan max) =>
        value > max ? max : value;
}
