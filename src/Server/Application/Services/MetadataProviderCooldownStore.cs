using System.Collections.Concurrent;

namespace K7.Server.Application.Services;

/// <summary>
/// In-process cooldown for Metadata-lane admission keys after a provider 429. While a provider is
/// cooling down, workers skip its tasks (spillover to other providers / lanes) instead of burning
/// attempts that will fail until the HTTP Retry-After window elapses.
/// </summary>
public sealed class MetadataProviderCooldownStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _cooldownUntil =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Extends the provider cooldown to at least <paramref name="utcNow"/> + <paramref name="retryAfter"/>.
    /// A later report wins; an earlier one does not shorten an existing cooldown.
    /// </summary>
    public void Report(string providerName, TimeSpan retryAfter, DateTimeOffset? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return;

        var delay = retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.FromSeconds(5);
        var now = utcNow ?? DateTimeOffset.UtcNow;
        var until = now.Add(delay);
        var key = Normalize(providerName);

        _cooldownUntil.AddOrUpdate(key, until, (_, existing) => until > existing ? until : existing);
    }

    public bool IsCoolingDown(string? providerName, DateTimeOffset? utcNow = null)
        => GetCooldownUntil(providerName, utcNow) is not null;

    public DateTimeOffset? GetCooldownUntil(string? providerName, DateTimeOffset? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return null;

        var now = utcNow ?? DateTimeOffset.UtcNow;
        var key = Normalize(providerName);
        if (!_cooldownUntil.TryGetValue(key, out var until))
            return null;

        if (until <= now)
        {
            _cooldownUntil.TryRemove(new KeyValuePair<string, DateTimeOffset>(key, until));
            return null;
        }

        return until;
    }

    /// <summary>Active provider -> cooldown end (UTC), expired entries purged.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> GetActiveCooldowns(DateTimeOffset? utcNow = null)
    {
        var now = utcNow ?? DateTimeOffset.UtcNow;
        Dictionary<string, DateTimeOffset> active = new(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, until) in _cooldownUntil)
        {
            if (until <= now)
            {
                _cooldownUntil.TryRemove(new KeyValuePair<string, DateTimeOffset>(key, until));
                continue;
            }

            active[key] = until;
        }

        return active;
    }

    public IReadOnlySet<string> GetCoolingDownProviders(DateTimeOffset? utcNow = null)
        => GetActiveCooldowns(utcNow).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string Normalize(string providerName) => providerName.Trim().ToLowerInvariant();
}
