using System.Text.Json;
using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Scrobbling;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Scrobbling.Services;

public class ScrobbleDispatcher(
    IApplicationDbContext context,
    IIdentityService identityService,
    IServerSettingsService settings,
    IScrobbleConfigProtector protector,
    IScrobbleQueue queue,
    ScrobblePayloadFactory payloadFactory,
    ISharedProfilePlaybackResolver sharedProfilePlaybackResolver,
    IScrobbleProgressThrottle progressThrottle,
    IScrobbleCompletionGate completionGate,
    ILogger<ScrobbleDispatcher> logger)
{
    private static readonly TimeSpan ProgressMinInterval = TimeSpan.FromSeconds(10);

    public Task DispatchAsync(PlaybackStateChangedEvent domainEvent, CancellationToken cancellationToken) =>
        DispatchStateChangedAsync(domainEvent, cancellationToken);

    private async Task DispatchStateChangedAsync(
        PlaybackStateChangedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // After a completion scrobble, Stop with Played:false reopens Yamtrack as In Progress.
        if (domainEvent.State is PlaybackState.Ended or PlaybackState.Idle)
        {
            var completedAt = await context.MediaPlaybackSessions
                .AsNoTracking()
                .Where(s => s.SessionId == domainEvent.SessionId)
                .Select(s => s.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (completedAt is not null)
            {
                logger.LogDebug(
                    "Skipping stop scrobble for completed session {SessionId}",
                    domainEvent.SessionId);
                return;
            }
        }

        await DispatchAsync(
            domainEvent.UserId,
            domainEvent.UserName,
            domainEvent.MediaId,
            domainEvent.State,
            domainEvent.Position,
            domainEvent.Duration,
            isCompleted: false,
            isProgressTick: false,
            domainEvent.SharedProfileId,
            cancellationToken);
    }

    public Task DispatchCompletedAsync(
        Guid sessionId,
        Guid userId,
        string? userName,
        Guid mediaId,
        double positionSeconds,
        double durationSeconds,
        Guid? sharedProfileId,
        CancellationToken cancellationToken)
    {
        if (!completionGate.TryClaim(sessionId))
        {
            logger.LogDebug(
                "Skipping duplicate completion scrobble for session {SessionId}",
                sessionId);
            return Task.CompletedTask;
        }

        return DispatchAsync(
            userId,
            userName,
            mediaId,
            PlaybackState.Ended,
            positionSeconds,
            durationSeconds,
            isCompleted: true,
            isProgressTick: false,
            sharedProfileId,
            cancellationToken);
    }

    public Task DispatchProgressAsync(
        Guid sessionId,
        Guid userId,
        string? userName,
        Guid mediaId,
        PlaybackState state,
        double positionSeconds,
        double durationSeconds,
        Guid? sharedProfileId,
        CancellationToken cancellationToken)
    {
        if (state is not PlaybackState.Playing)
            return Task.CompletedTask;

        if (!progressThrottle.TryAcquire(sessionId, ProgressMinInterval))
            return Task.CompletedTask;

        return DispatchAsync(
            userId,
            userName,
            mediaId,
            state,
            positionSeconds,
            durationSeconds,
            isCompleted: false,
            isProgressTick: true,
            sharedProfileId,
            cancellationToken);
    }

    private async Task DispatchAsync(
        Guid userId,
        string? userName,
        Guid mediaId,
        PlaybackState state,
        double positionSeconds,
        double durationSeconds,
        bool isCompleted,
        bool isProgressTick,
        Guid? sharedProfileId,
        CancellationToken cancellationToken)
    {
        var json = await settings.GetAsync(ServerSettingKeys.Scrobbling, cancellationToken);
        var config = string.IsNullOrEmpty(json)
            ? new ScrobblingSettingsDto()
            : JsonSerializer.Deserialize<ScrobblingSettingsDto>(json) ?? new ScrobblingSettingsDto();

        if (!config.Enabled)
            return;

        var targets = await ResolveTargetsAsync(userId, userName, sharedProfileId, cancellationToken);
        if (targets.Count == 0)
            return;

        var basePayload = await payloadFactory.CreateAsync(
            userId,
            userName,
            mediaId,
            state,
            positionSeconds,
            durationSeconds,
            isCompleted,
            isProgressTick,
            cancellationToken);
        if (basePayload is null)
            return;

        foreach (var (targetUserId, targetUserName) in targets)
        {
            if (!await UserCapabilityEvaluator.HasAsync(
                    context, identityService, targetUserId, Capability.CanScrobble, cancellationToken))
                continue;

            var accounts = await context.UserScrobblerAccounts
                .AsNoTracking()
                .Where(a => a.UserId == targetUserId && a.IsEnabled)
                .ToListAsync(cancellationToken);

            if (accounts.Count == 0)
                continue;

            var payload = basePayload with { UserId = targetUserId, UserName = targetUserName };

            foreach (var account in accounts)
            {
                var configJson = protector.Unprotect(account.ConfigJson);
                var allowed = ScrobblerAccountMapper.AllowedMediaTypes(
                    account.Provider,
                    ScrobblerAccountMapper.ReadWebhookPresetId(configJson));
                if (!allowed.Contains(payload.MediaType))
                    continue;
                if (!ScrobbleEligibility.AllowsMediaType(account.MediaTypes, payload.MediaType))
                    continue;

                if (ShouldSkip(account, configJson, payload))
                    continue;

                queue.Enqueue(new ScrobbleWorkItem
                {
                    AccountId = account.Id,
                    ConfigJson = configJson,
                    Provider = account.Provider,
                    Payload = payload
                });

                logger.LogDebug(
                    "Queued scrobble for account {AccountId} provider {Provider} user {UserId}",
                    account.Id, account.Provider, targetUserId);
            }
        }
    }

    private async Task<IReadOnlyList<(Guid UserId, string? UserName)>> ResolveTargetsAsync(
        Guid actingUserId,
        string? actingUserName,
        Guid? sharedProfileId,
        CancellationToken cancellationToken)
    {
        if (sharedProfileId is null)
            return [(actingUserId, actingUserName)];

        var viewingGroup = await sharedProfilePlaybackResolver.ResolveAsync(
            sharedProfileId.Value, actingUserId, cancellationToken);
        if (viewingGroup is null)
            return [(actingUserId, actingUserName)];

        var memberIds = viewingGroup.CoViewerUserIds
            .Append(actingUserId)
            .Distinct()
            .ToList();

        var users = await context.Users
            .AsNoTracking()
            .Where(u => memberIds.Contains(u.Id))
            .Select(u => new { u.Id, u.IdentityUserId, u.DisplayName })
            .ToListAsync(cancellationToken);

        var results = new List<(Guid UserId, string? UserName)>(memberIds.Count);
        foreach (var memberId in memberIds)
        {
            if (memberId == actingUserId)
            {
                results.Add((actingUserId, actingUserName));
                continue;
            }

            var user = users.FirstOrDefault(u => u.Id == memberId);
            string? name = user?.DisplayName;
            if (!string.IsNullOrEmpty(user?.IdentityUserId))
                name = await identityService.GetUserNameAsync(user.IdentityUserId) ?? name;

            results.Add((memberId, name));
        }

        return results;
    }

    private static bool ShouldSkip(
        Domain.Entities.Notifications.UserScrobblerAccount account,
        string configJson,
        ScrobblePayload payload)
    {
        if (payload.IsProgressTick)
        {
            if (account.Provider != ScrobblerProvider.Webhook)
                return true;

            return !IsWebhookEventEnabled(configJson, ScrobbleWebhookEvents.Progress);
        }

        if (payload.IsCompleted)
        {
            if (account.Provider == ScrobblerProvider.Webhook)
                return !IsWebhookEventEnabled(configJson, ScrobbleWebhookEvents.Scrobble);
            return false;
        }

        return account.Provider switch
        {
            ScrobblerProvider.LastFm or ScrobblerProvider.ListenBrainz =>
                payload.State is PlaybackState.Playing
                    ? !account.IncludeNowPlaying
                    : payload.State is PlaybackState.Ended
                        ? !ScrobbleEligibility.MeetsListenThreshold(payload.ProgressPercent, payload.DurationSeconds)
                        : true,
            ScrobblerProvider.Trakt => payload.MediaType is MediaType.MusicTrack or MediaType.MusicAlbum or MediaType.MusicArtist,
            ScrobblerProvider.Webhook => !IsWebhookEventEnabled(
                configJson,
                ScrobbleWebhookEvents.ForPlaybackState(payload.State, payload.IsCompleted, payload.IsProgressTick)),
            _ => true
        };
    }

    private static bool IsWebhookEventEnabled(string configJson, string? eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(configJson);
            IEnumerable<string>? events = null;
            if (doc.RootElement.TryGetProperty("events", out var eventsNode)
                && eventsNode.ValueKind == JsonValueKind.Array)
            {
                events = eventsNode.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!);
            }

            // Legacy accounts without events: keep prior IncludeNowPlaying-like behavior via all events on.
            if (events is null)
                return true;

            return ScrobbleWebhookEvents.IsEnabled(events, eventKey);
        }
        catch (JsonException)
        {
            return true;
        }
    }
}
