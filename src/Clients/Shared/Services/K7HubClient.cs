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

    /// <summary>
    /// Raised when the server pushes a playback progress update.
    /// </summary>
    public event Action<Guid, double, bool>? ProgressUpdated;

    /// <summary>
    /// Ensures the hub connection is started. Safe to call multiple times.
    /// </summary>
    /// <param name="baseUri">The base URI of the application (used to build the hub URL).</param>
    /// <param name="userId">The identity user ID to associate with this connection.</param>
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

            // Dispose any previous connection
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
