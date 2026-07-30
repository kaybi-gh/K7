using System.Collections.Concurrent;

namespace K7.Server.Infrastructure.ExternalServices.Federation;

/// <summary>
/// Process-wide cache for peer client-credentials tokens. HLS proxies one token request
/// per segment without this, which trips the peer auth rate limit (20/min).
/// </summary>
public sealed class PeerAccessTokenCache
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public bool TryGet(string baseUrl, string clientId, out string? accessToken)
    {
        accessToken = null;
        var key = BuildKey(baseUrl, clientId);
        if (!_entries.TryGetValue(key, out var entry))
            return false;

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _entries.TryRemove(key, out _);
            return false;
        }

        accessToken = entry.AccessToken;
        return true;
    }

    public void Set(string baseUrl, string clientId, string accessToken, int expiresInSeconds)
    {
        var lifetime = expiresInSeconds > 0 ? expiresInSeconds : 3600;
        // Refresh a minute early (or at half-life for very short tokens).
        var skewSeconds = Math.Clamp(lifetime / 10, 30, 60);
        var usableSeconds = Math.Max(lifetime - skewSeconds, 1);
        var key = BuildKey(baseUrl, clientId);
        _entries[key] = new Entry(accessToken, DateTime.UtcNow.AddSeconds(usableSeconds));
    }

    public async Task<T> WithLockAsync<T>(
        string baseUrl,
        string clientId,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var key = BuildKey(baseUrl, clientId);
        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await action(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private static string BuildKey(string baseUrl, string clientId) =>
        $"{baseUrl.TrimEnd('/')}|{clientId}";

    private sealed record Entry(string AccessToken, DateTime ExpiresAtUtc);
}
