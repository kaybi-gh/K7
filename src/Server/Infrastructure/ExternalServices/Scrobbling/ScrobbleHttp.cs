using System.Collections.Concurrent;
using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Infrastructure.ExternalServices.Scrobbling;

internal static class ScrobbleHttp
{
    public static async Task<ScrobbleSendResult> FromResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        if (response.IsSuccessStatusCode)
            return ScrobbleSendResult.Ok();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        body = body.Trim();
        if (body.Length > 240)
            body = body[..240] + "...";

        var detail = string.IsNullOrWhiteSpace(body)
            ? response.ReasonPhrase ?? "no body"
            : body;

        return ScrobbleSendResult.Fail($"HTTP {(int)response.StatusCode}: {detail}");
    }
}

/// <summary>
/// Per-account cooldown for ListenBrainz <c>playing_now</c> (completions always pass).
/// </summary>
internal sealed class ListenBrainzPlayingNowThrottle
{
    public static ListenBrainzPlayingNowThrottle Instance { get; } = new();

    private readonly ConcurrentDictionary<Guid, long> _lastTicks = new();
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(30);

    public bool TryAcquire(Guid accountId)
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var min = MinInterval.Ticks;

        while (true)
        {
            if (!_lastTicks.TryGetValue(accountId, out var last))
            {
                if (_lastTicks.TryAdd(accountId, now))
                    return true;
                continue;
            }

            if (now - last < min)
                return false;

            if (_lastTicks.TryUpdate(accountId, now, last))
                return true;
        }
    }
}
