using System.Collections.Concurrent;
using K7.Server.Web.Endpoints.Hubs;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

internal sealed class MediaNotificationBatcher : IAsyncDisposable
{
    private readonly IHubContext<K7Hub, IK7HubClient> _hubContext;
    private readonly ILogger<MediaNotificationBatcher> _logger;
    private readonly ConcurrentQueue<MediaBatchItem> _queue = new();
    private readonly Timer _idleTimer;
    private readonly Timer _maxWaitTimer;
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private const int MaxBatchSize = 50;
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxWaitTimeout = TimeSpan.FromSeconds(30);

    public MediaNotificationBatcher(IHubContext<K7Hub, IK7HubClient> hubContext, ILogger<MediaNotificationBatcher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
        _idleTimer = new Timer(OnIdleTimeout, null, Timeout.Infinite, Timeout.Infinite);
        _maxWaitTimer = new Timer(OnMaxWaitTimeout, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Enqueue(MediaBatchItem item)
    {
        var wasEmpty = _queue.IsEmpty;
        _queue.Enqueue(item);

        _idleTimer.Change(IdleTimeout, Timeout.InfiniteTimeSpan);

        if (wasEmpty)
        {
            _maxWaitTimer.Change(MaxWaitTimeout, Timeout.InfiniteTimeSpan);
        }

        if (_queue.Count >= MaxBatchSize)
        {
            _ = FlushAsync();
        }
    }

    private void OnIdleTimeout(object? state) => _ = FlushAsync();
    private void OnMaxWaitTimeout(object? state) => _ = FlushAsync();

    private async Task FlushAsync()
    {
        if (!await _flushLock.WaitAsync(0)) return;

        try
        {
            var items = new List<MediaBatchItem>();
            while (items.Count < MaxBatchSize && _queue.TryDequeue(out var item))
            {
                items.Add(item);
            }

            if (items.Count == 0) return;

            _idleTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _maxWaitTimer.Change(Timeout.Infinite, Timeout.Infinite);

            _logger.LogDebug("Flushing {Count} batched media notifications", items.Count);
            await _hubContext.Clients.All.ReceiveMediaBatchAdded(items);

            if (!_queue.IsEmpty)
            {
                _idleTimer.Change(IdleTimeout, Timeout.InfiniteTimeSpan);
                _maxWaitTimer.Change(MaxWaitTimeout, Timeout.InfiniteTimeSpan);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing media notification batch");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _idleTimer.DisposeAsync();
        await _maxWaitTimer.DisposeAsync();
        await FlushAsync();
        _flushLock.Dispose();
    }
}
