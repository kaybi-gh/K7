using Microsoft.AspNetCore.SignalR.Client;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Singleton service that manages a persistent SignalR connection to the K7 hub.
/// Survives page navigation.
/// </summary>
public sealed class K7HubClient : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private bool _started;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public HubConnectionState State => _hubConnection?.State ?? HubConnectionState.Disconnected;

    public event Action<HubConnectionState>? ConnectionStateChanged;
    public event Action<Guid, double, bool>? ProgressUpdated;
    public event Action<Guid, string?, string>? MediaAdded;
    public event Action<Guid, int>? LibraryScanCompleted;
    public event Action? BackgroundTaskUpdated;

    public async Task EnsureStartedAsync(Uri baseUri, string userId, string? deviceId = null, string? accessToken = null)
    {
        if (string.IsNullOrEmpty(userId)) return;

        if (_started && _hubConnection?.State == HubConnectionState.Connected)
            return;

        await _lock.WaitAsync();
        try
        {
            if (_started && _hubConnection?.State == HubConnectionState.Connected)
                return;

            if (_hubConnection is not null)
            {
                await _hubConnection.DisposeAsync();
            }

            var query = $"/hub?userId={Uri.EscapeDataString(userId)}";
            if (!string.IsNullOrEmpty(deviceId))
            {
                query += $"&deviceId={Uri.EscapeDataString(deviceId)}";
            }

            var hubUrl = new Uri(baseUri, query);

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        options.Headers["Authorization"] = $"Bearer {accessToken}";
                    }
                })
                .WithAutomaticReconnect(new InfiniteRetryPolicy())
                .Build();

            _hubConnection.Reconnecting += _ =>
            {
                ConnectionStateChanged?.Invoke(HubConnectionState.Reconnecting);
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += _ =>
            {
                ConnectionStateChanged?.Invoke(HubConnectionState.Connected);
                return Task.CompletedTask;
            };

            _hubConnection.Closed += _ =>
            {
                ConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
                return Task.CompletedTask;
            };

            _hubConnection.On<Guid, double, bool>("ReceivePlaybackProgress", (mediaId, progress, isCompleted) =>
            {
                ProgressUpdated?.Invoke(mediaId, progress, isCompleted);
            });

            _hubConnection.On<Guid, string?, string>("ReceiveMediaAdded", (mediaId, title, mediaType) =>
            {
                MediaAdded?.Invoke(mediaId, title, mediaType);
            });

            _hubConnection.On<Guid, int>("ReceiveLibraryScanCompleted", (libraryId, addedCount) =>
            {
                LibraryScanCompleted?.Invoke(libraryId, addedCount);
            });

            _hubConnection.On("ReceiveBackgroundTaskUpdated", () =>
            {
                BackgroundTaskUpdated?.Invoke();
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _hubConnection.StartAsync(cts.Token);
            _started = true;
            ConnectionStateChanged?.Invoke(HubConnectionState.Connected);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }

        _lock.Dispose();
    }

    private sealed class InfiniteRetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
            TimeSpan.FromSeconds(Math.Min(retryContext.PreviousRetryCount * 2, 10));
    }
}
