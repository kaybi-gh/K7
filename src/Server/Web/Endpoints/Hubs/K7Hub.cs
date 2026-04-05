using System.Globalization;
using System.Security.Claims;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Endpoints.Hubs;

/// <summary>
/// Central SignalR hub for all real-time communication between the server and connected clients.
/// Each connection is associated with a user identity and added to a user-scoped group.
/// The identity is resolved from the auth cookie when available, with a fallback to a query string
/// parameter for environments where cookies are not transmitted (e.g. Blazor WASM WebSocket connections).
/// </summary>
public class K7Hub(ISender sender, IApplicationDbContext dbContext, ILogger<K7Hub> logger) : Hub<IK7HubClient>
{
    public override async Task OnConnectedAsync()
    {
        var identityUserId = ResolveIdentityUserId();

        logger.LogDebug("Hub connection established: identityUserId='{IdentityUserId}', connectionId='{ConnectionId}'", identityUserId, Context.ConnectionId);

        if (string.IsNullOrEmpty(identityUserId))
        {
            logger.LogWarning("No identity on hub connection, aborting. ConnectionId='{ConnectionId}'", Context.ConnectionId);
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, identityUserId);

        // Update device LastSeen timestamp
        var httpContext = Context.GetHttpContext();
        if (Guid.TryParse(httpContext?.Request.Query["deviceId"], out var deviceId))
        {
            await dbContext.Devices
                .Where(d => d.Id == deviceId)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastSeen, DateTimeOffset.UtcNow));
        }

        // If a streaming session was requested, set up the session group as well
        if (Guid.TryParse(httpContext?.Request.Query["indexedFileId"], out Guid indexedFileId))
        {
            double position = 0;
            if (double.TryParse(httpContext!.Request.Query["position"], NumberStyles.Float, CultureInfo.InvariantCulture, out double providedPosition))
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
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var identityUserId = ResolveIdentityUserId();

        if (!string.IsNullOrEmpty(identityUserId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, identityUserId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // --- Client-to-server methods (streaming session) ---

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
        var uri = await sender.Send(new GetStreamUriQuery { Id = indexedFileId, DeviceId = deviceId });
        await Clients.Caller.ReceiveIndexedFileStreamUri(uri);
    }

    /// <summary>
    /// Resolves the identity user ID from cookie auth or query string fallback.
    /// </summary>
    private string? ResolveIdentityUserId()
    {
        var identityUserId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(identityUserId))
        {
            var httpContext = Context.GetHttpContext();
            identityUserId = httpContext?.Request.Query["userId"].ToString();
        }

        return string.IsNullOrEmpty(identityUserId) ? null : identityUserId;
    }
}
