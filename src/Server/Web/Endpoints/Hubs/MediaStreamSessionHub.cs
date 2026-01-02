using System.Globalization;
using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace K7.Server.Web.Endpoints.Hubs;

public class MediaStreamSessionHub : Hub<IMediaStreamSession>
{
    private readonly ISender _sender;

    public MediaStreamSessionHub(ISender sender)
    {
        _sender = sender;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext() ?? throw new InvalidOperationException();

        if (!Guid.TryParse(httpContext.Request.Query["indexedFileId"], out Guid indexedFileId))
        {
            Context.Abort();
            return;
        }

        double position = 0;
        if (double.TryParse(httpContext.Request.Query["position"], NumberStyles.Float, CultureInfo.InvariantCulture, out double providedPosition))
        {
            position = providedPosition;
        }

        var session = new StreamingSessionDto
        {
            Id = Guid.NewGuid(),
            IndexedFileId = indexedFileId,
            State = PlaybackState.Idle,
            Position = position,
            PlaybackSettings = new()
        };

        await Groups.AddToGroupAsync(Context.ConnectionId, session.Id.ToString());
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        return base.OnDisconnectedAsync(exception);
    }

    public Task ChangePlaybackSettings(Guid streamId, PlaybackSettingsDto playbackSettings)
    {
        return Clients.Caller.ChangePlaybackSettings(streamId, playbackSettings);
    }

    public Task SendPlaybackState(Guid streamId, PlaybackState state, double position)
    {
        return Clients.Caller.SendPlaybackState(streamId, state, position);
    }

    public async Task SendIndexedFileStreamUri(Guid streamId, Guid indexedFileId, Guid deviceId, PlaybackSettingsDto playbackSettings)
    {
        var uri = await _sender.Send(new GetStreamUriQuery { Id = indexedFileId, DeviceId = deviceId });
        await Clients.Caller.ReceiveIndexedFileStreamUri(uri);
    }
}

