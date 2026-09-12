using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Scrobbling.Services;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Enums;

namespace K7.Server.Infrastructure.ExternalServices.Scrobbling;

public sealed class LastFmScrobbleDestination(LastFmClient lastFmClient) : IScrobbleDestination
{
    public ScrobblerProvider Provider => ScrobblerProvider.LastFm;

    public async Task<ScrobbleSendResult> SendAsync(
        UserScrobblerAccount account,
        string configJson,
        ScrobblePayload payload,
        CancellationToken cancellationToken = default)
    {
        var config = ScrobbleJson.Deserialize<LastFmConfig>(configJson);
        if (config is null || string.IsNullOrWhiteSpace(config.SessionKey))
            return ScrobbleSendResult.Fail("Last.fm session is missing. Reconnect the account.");

        try
        {
            if (payload.IsCompleted || payload.State is PlaybackState.Ended)
            {
                await lastFmClient.ScrobbleAsync(config.SessionKey, payload, cancellationToken);
                return ScrobbleSendResult.Ok();
            }

            if (payload.State is PlaybackState.Playing)
            {
                await lastFmClient.UpdateNowPlayingAsync(config.SessionKey, payload, cancellationToken);
                return ScrobbleSendResult.Ok();
            }

            return ScrobbleSendResult.Ok();
        }
        catch (Exception ex)
        {
            return ScrobbleSendResult.Fail(ex.Message);
        }
    }

    public async Task<ScrobbleSendResult> TestAsync(
        UserScrobblerAccount account,
        string configJson,
        CancellationToken cancellationToken = default)
    {
        var config = ScrobbleJson.Deserialize<LastFmConfig>(configJson);
        if (config is null || string.IsNullOrWhiteSpace(config.SessionKey))
            return ScrobbleSendResult.Fail("Last.fm session is missing. Reconnect the account.");

        try
        {
            await lastFmClient.TestSessionAsync(config.SessionKey, cancellationToken);
            return ScrobbleSendResult.Ok();
        }
        catch (Exception ex)
        {
            return ScrobbleSendResult.Fail(ex.Message);
        }
    }

    private sealed class LastFmConfig
    {
        public string? SessionKey { get; set; }
    }
}
