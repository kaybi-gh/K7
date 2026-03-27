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

    public event Action<Guid, double, bool>? ProgressUpdated;
    public event Action<Guid, string?, string>? MediaAdded;
    public event Action<Guid, int>? LibraryScanCompleted;

    public async Task EnsureStartedAsync(Uri baseUri, string userId)
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

            var hubUrl = new Uri(baseUri, $"/hub?userId={Uri.EscapeDataString(userId)}");

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

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

            await _hubConnection.StartAsync();
            _started = true;
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
}
