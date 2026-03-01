using K7.Clients.Shared.Domain.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace K7.Clients.MAUI.Services;
public class MediaSessionService : IMediaStreamSession
{
    private readonly NavigationManager _navigationManager;
    private readonly IPlayerService _playerService;
    private HubConnection? _hubConnection;

    public MediaSessionService(NavigationManager navigationManager, IPlayerService playerService)
    {
        _navigationManager = navigationManager;
        _playerService = playerService;
    }

    public async Task StartConnectionAsync(/*Guid userId, Guid deviceId, Guid mediaId*/)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            _hubConnection = null;
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_navigationManager.ToAbsoluteUri($"/hub"))
            .Build();

        await _hubConnection.StartAsync();
    }

    public async Task StopConnectionAsync()
    {
        await _hubConnection!.StopAsync();
    }

    public async Task PlayMedia(Guid mediaId)
    {
        await _hubConnection!.SendAsync("ControlMedia", "play", mediaId);
    }


    public async Task PauseMedia(Guid mediaId)
    {
        await _hubConnection!.SendAsync("ControlMedia", "pause", mediaId);
    }

    public async Task StopMedia(Guid mediaId)
    {
        await _hubConnection!.SendAsync("ControlMedia", "stop", mediaId);
    }

    public async Task ChangeQuality(string mediaId, string newUrl)
    {
        await _hubConnection!.SendAsync("UpdateStreamQuality", mediaId, newUrl);
    }

    public async Task SyncPlayback(string mediaId, string state, double timestamp)
    {
        await _hubConnection!.SendAsync("SyncPlaybackState", mediaId, state, timestamp);
    }

    public void OnReceiveControlCommand(Func<string, string, Task> onCommandReceived)
    {
        _hubConnection!.On("ReceiveControlCommand", onCommandReceived);
    }

    public void OnReceiveNewStreamUrl(Func<string, string, Task> onNewStreamUrlReceived)
    {
        _hubConnection!.On("ReceiveNewStreamUrl", onNewStreamUrlReceived);
    }

    public void OnSyncPlayback(Func<string, string, double, Task> onSyncReceived)
    {
        _hubConnection!.On("SyncPlayback", onSyncReceived);
    }

    public Task ChangePlaybackSettings(Guid streamId, PlaybackSettingsDto playbackSettings)
    {
        throw new NotImplementedException();
    }

    public Task SendPlaybackState(Guid streamId, PlaybackState state, double position)
    {
        throw new NotImplementedException();
    }

    public Task ReceiveIndexedFileStreamUri(IndexedFileStreamUri streamUri)
    {
        throw new NotImplementedException();
    }
}

