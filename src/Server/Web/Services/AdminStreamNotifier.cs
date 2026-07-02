using K7.Server.Application.Services;
using K7.Server.Web.Endpoints.Hubs;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

internal sealed class AdminStreamNotifier(
    IHubContext<K7Hub, IK7HubClient> hubContext,
    IActiveStreamTracker activeStreamTracker,
    ILogger<AdminStreamNotifier> logger) : BackgroundService
{
    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(BroadcastInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var streams = activeStreamTracker.GetActiveStreams()
                    .Select(s => new ActiveStreamDto
                    {
                        ConnectionId = s.SessionId.ToString(),
                        UserId = s.UserId,
                        UserName = s.UserName,
                        MediaId = s.MediaId,
                        MediaTitle = s.MediaTitle,
                        MediaType = s.MediaType,
                        ParentId = s.ParentId,
                        DeviceId = s.DeviceId,
                        DeviceName = s.DeviceName,
                        DeviceType = s.DeviceType,
                        ThumbnailUrl = s.ThumbnailUrl,
                        StreamDecision = s.StreamDecision,
                        StartedAt = s.StartedAt,
                        Position = s.Position,
                        Duration = s.Duration,
                        State = s.State,
                        ViewingGroupName = s.ViewingGroupName
                    })
                    .ToList();

                await hubContext.Clients.Group(K7Hub.AdminStreamsGroup)
                    .ReceiveActiveStreamsUpdated(streams);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to broadcast active streams to admin group");
            }
        }
    }
}
