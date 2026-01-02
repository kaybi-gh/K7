using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;

namespace K7.Clients.MAUI.Services;

public class MediaStreamSessionHubClient : IMediaStreamSession
{
    private readonly HubConnection _hubConnection;

    public MediaStreamSessionHubClient()
    {
        var hubUrl = "";
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        // Callbacks
        _hubConnection.On<Uri>("ReceiveIndexedFileStreamUri", ReceiveIndexedFileStreamUri);
    }

    public async Task ConnectAsync() => await _hubConnection.StartAsync();

    public async Task DisconnectAsync() => await _hubConnection.StopAsync();

    public Task ChangePlaybackSettings(Guid streamId, PlaybackSettingsDto playbackSettings)
    {
        throw new NotImplementedException();
    }

    public Task SendPlaybackState(Guid streamId, PlaybackState state, double position)
    {
        throw new NotImplementedException();
    }

    public Task ReceiveIndexedFileStreamUri(Uri streamUri)
    {
        Console.WriteLine($"Received indexed file stream uri: {streamUri.OriginalString}");
        return Task.CompletedTask;
    }

    public Task ReceiveIndexedFileStreamUri(IndexedFileStreamUri streamUri)
    {
        throw new NotImplementedException();
    }
}
