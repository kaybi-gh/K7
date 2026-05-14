using System.Collections.Concurrent;

namespace K7.Server.Application.Services;

public class OutboundRateLimiter
{
    private readonly ConcurrentDictionary<string, HostRateState> _hosts = new(StringComparer.OrdinalIgnoreCase);

    public async Task WaitAsync(string host, CancellationToken cancellationToken = default)
    {
        if (!_hosts.TryGetValue(host, out var state))
        {
            return;
        }

        await state.Semaphore.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var earliest = state.LastRequestTime + state.MinInterval;

            // If a Retry-After was received, respect it
            if (state.RetryAfter > earliest)
            {
                earliest = state.RetryAfter;
            }

            var delay = earliest - now;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            state.LastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            state.Semaphore.Release();
        }
    }

    public void ReportRetryAfter(string host, TimeSpan retryAfter)
    {
        if (_hosts.TryGetValue(host, out var state))
        {
            state.RetryAfter = DateTime.UtcNow + retryAfter;
        }
    }

    public void ConfigureHost(string host, TimeSpan minInterval)
    {
        _hosts[host] = new HostRateState(minInterval);
    }

    private sealed class HostRateState
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public TimeSpan MinInterval { get; }
        public DateTime LastRequestTime { get; set; } = DateTime.MinValue;
        public DateTime RetryAfter { get; set; } = DateTime.MinValue;

        public HostRateState(TimeSpan minInterval)
        {
            MinInterval = minInterval;
        }
    }
}
