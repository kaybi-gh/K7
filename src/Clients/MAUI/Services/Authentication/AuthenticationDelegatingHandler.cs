using System.Net;
using System.Net.Http.Headers;
using K7.Clients.Shared.Interfaces;
using K7.Shared;
using Microsoft.AspNetCore.Components.Authorization;

namespace K7.Clients.MAUI.Services.Authentication;

public class AuthenticationDelegatingHandler : DelegatingHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private int _logoutTriggered;

    public AuthenticationDelegatingHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Wait for startup session restore before attaching stored tokens. Without this,
        // an expired access token can be sent while restore is refreshing, causing a
        // concurrent /connect/token call and OpenIddict refresh-token replay revocation.
        var authProvider = _serviceProvider.GetRequiredService<AuthenticationStateProvider>();
        await authProvider.GetAuthenticationStateAsync();

        // Buffer the body so a 401 retry can resend POSTs (JsonContent is one-shot otherwise).
        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync(cancellationToken);

        // Pre-set the auth token before sending (avoids 401 race on concurrent requests)
        if (request.Headers.Authorization is null)
        {
            var deviceStorage = _serviceProvider.GetRequiredService<IDeviceStorageService>();
            var token = deviceStorage.Get(PreferenceKeys.ACCESS_TOKEN);
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        AttachSharedProfileHeader(request);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode is not HttpStatusCode.Unauthorized)
            return response;

        var rejectedToken = request.Headers.Authorization?.Parameter;

        // Serialize refresh so concurrent 401s wait and retry with the new token instead of
        // returning Unauthorized while only the first request refreshes (Movie page race).
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            var deviceStorage = _serviceProvider.GetRequiredService<IDeviceStorageService>();
            var tokenAfterWait = deviceStorage.Get(PreferenceKeys.ACCESS_TOKEN);

            // Another waiter may have already refreshed successfully - retry first.
            if (!string.IsNullOrEmpty(tokenAfterWait)
                && !string.Equals(tokenAfterWait, rejectedToken, StringComparison.Ordinal))
            {
                response.Dispose();
                var earlyRetry = await CloneAndRetryAsync(request, tokenAfterWait, cancellationToken);
                if (earlyRetry.StatusCode is not HttpStatusCode.Unauthorized)
                {
                    Interlocked.Exchange(ref _logoutTriggered, 0);
                    return earlyRetry;
                }

                earlyRetry.Dispose();
                rejectedToken = tokenAfterWait;
            }

            var customAuthProvider = _serviceProvider.GetRequiredService<ICustomAuthenticationStateProvider>();

            // Force the token endpoint when this exact Bearer was rejected (do not trust
            // client-side JWT expiry alone - server may disagree after long sleep / skew).
            if (await customAuthProvider.TryRefreshAsync(cancellationToken, rejectedAccessToken: rejectedToken))
            {
                Interlocked.Exchange(ref _logoutTriggered, 0);
                var newToken = deviceStorage.Get(PreferenceKeys.ACCESS_TOKEN);

                if (!string.IsNullOrEmpty(newToken))
                {
                    response.Dispose();
                    var retry = await CloneAndRetryAsync(request, newToken, cancellationToken);

                    // Refresh worked: return the retry outcome (success or business error).
                    // Only treat a second 401 as a hard auth failure.
                    if (retry.StatusCode is not HttpStatusCode.Unauthorized)
                        return retry;

                    response = retry;
                }
            }

            // Refresh failed, or retry still unauthorized after a real token rotation.
            var authStateProvider = _serviceProvider.GetRequiredService<AuthenticationStateProvider>();
            var authState = await authStateProvider.GetAuthenticationStateAsync();
            var isOfflineSession = authState.User.Identity?.AuthenticationType == "Offline";

            if (!isOfflineSession && Interlocked.CompareExchange(ref _logoutTriggered, 1, 0) == 0)
            {
                var authProvider2 = _serviceProvider.GetRequiredService<ICustomAuthenticationStateProvider>();
                await authProvider2.LogoutAsync(cancellationToken);
            }
        }
        finally
        {
            _refreshGate.Release();
        }

        return response;
    }

    private void AttachSharedProfileHeader(HttpRequestMessage request)
    {
        if (request.Headers.Contains(HttpHeaderNames.SharedProfileId))
            return;

        var session = _serviceProvider.GetService<ISharedProfileSessionService>();
        if (session?.ActiveGroupId is { } profileId)
            request.Headers.TryAddWithoutValidation(HttpHeaderNames.SharedProfileId, profileId.ToString());
    }

    private async Task<HttpResponseMessage> CloneAndRetryAsync(HttpRequestMessage original, string accessToken, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        foreach (var header in original.Headers)
        {
            if (!header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (original.Content is not null)
        {
            var contentBytes = await original.Content.ReadAsByteArrayAsync(cancellationToken);
            var newContent = new ByteArrayContent(contentBytes);
            foreach (var header in original.Content.Headers)
                newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
            clone.Content = newContent;
        }

        AttachSharedProfileHeader(clone);
        return await base.SendAsync(clone, cancellationToken);
    }
}
