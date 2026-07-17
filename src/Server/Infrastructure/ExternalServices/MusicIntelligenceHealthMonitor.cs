using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Events;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Infrastructure.ExternalServices;

/// <summary>
/// Shared reachability cache for AudioMuse so status checks and feature calls do not each pay a full timeout when the service is down.
/// Publishes <see cref="MusicIntelligenceUnavailableEvent"/> only when transitioning from reachable (true) to unreachable (false), to avoid spam.
/// </summary>
public sealed class MusicIntelligenceHealthMonitor(IServiceScopeFactory scopeFactory)
{
    private static readonly TimeSpan PositiveTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan NegativeTtl = TimeSpan.FromSeconds(15);

    private readonly SemaphoreSlim _probeGate = new(1, 1);
    private readonly object _stateGate = new();

    private bool? _reachable;
    private DateTimeOffset _checkedAt = DateTimeOffset.MinValue;

    public bool? CachedReachable
    {
        get
        {
            lock (_stateGate)
            {
                return IsCacheFresh() ? _reachable : null;
            }
        }
    }

    public void MarkUnreachable(string? reason = null)
    {
        if (SetReachable(false))
            _ = PublishUnavailableAsync(reason);
    }

    public void MarkReachable()
    {
        SetReachable(true);
    }

    public async Task<bool> GetReachableAsync(
        Func<CancellationToken, Task<bool>> probe,
        CancellationToken cancellationToken = default)
    {
        lock (_stateGate)
        {
            if (IsCacheFresh() && _reachable is bool cached)
                return cached;
        }

        await _probeGate.WaitAsync(cancellationToken);
        try
        {
            lock (_stateGate)
            {
                if (IsCacheFresh() && _reachable is bool cached)
                    return cached;
            }

            var ok = await probe(cancellationToken);
            var transitionedDown = SetReachable(ok);
            if (transitionedDown)
                _ = PublishUnavailableAsync("health probe failed");

            return ok;
        }
        finally
        {
            _probeGate.Release();
        }
    }

    private bool SetReachable(bool value)
    {
        lock (_stateGate)
        {
            var transitionedToUnreachable = !value && _reachable == true;
            _reachable = value;
            _checkedAt = DateTimeOffset.UtcNow;
            return transitionedToUnreachable;
        }
    }

    private async Task PublishUnavailableAsync(string? reason)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();
            await publisher.PublishAsync(new MusicIntelligenceUnavailableEvent(reason));
        }
        catch
        {
            // Health monitor must not throw into callers.
        }
    }

    private bool IsCacheFresh()
    {
        if (_reachable is null)
            return false;

        var ttl = _reachable == true ? PositiveTtl : NegativeTtl;
        return DateTimeOffset.UtcNow - _checkedAt < ttl;
    }
}
