using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Scrobbling.Services;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Enums;

namespace K7.Server.Infrastructure.ExternalServices.Scrobbling;

public sealed class TraktScrobbleDestination(TraktClient traktClient) : IScrobbleDestination
{
    public ScrobblerProvider Provider => ScrobblerProvider.Trakt;

    public async Task<ScrobbleSendResult> SendAsync(
        UserScrobblerAccount account,
        string configJson,
        ScrobblePayload payload,
        CancellationToken cancellationToken = default)
    {
        var config = ScrobbleJson.Deserialize<TraktConfig>(configJson);
        if (config is null || string.IsNullOrWhiteSpace(config.AccessToken))
            return ScrobbleSendResult.Fail("Trakt access token is missing. Reconnect the account.");

        try
        {
            var success = await traktClient.ScrobbleAsync(config.AccessToken, payload, cancellationToken);
            return success
                ? ScrobbleSendResult.Ok()
                : ScrobbleSendResult.Fail("Trakt rejected the scrobble request.");
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
        var config = ScrobbleJson.Deserialize<TraktConfig>(configJson);
        if (config is null || string.IsNullOrWhiteSpace(config.AccessToken))
            return ScrobbleSendResult.Fail("Trakt access token is missing. Reconnect the account.");

        try
        {
            var success = await traktClient.TestAsync(config.AccessToken, cancellationToken);
            return success
                ? ScrobbleSendResult.Ok()
                : ScrobbleSendResult.Fail("Trakt rejected the credentials. Reconnect the account.");
        }
        catch (Exception ex)
        {
            return ScrobbleSendResult.Fail(ex.Message);
        }
    }

    private sealed class TraktConfig
    {
        public string? AccessToken { get; set; }
    }
}
