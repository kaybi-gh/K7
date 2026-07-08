using System.Net;
using System.Net.Http.Headers;
using K7.Clients.Shared.Interfaces;
using K7.Shared;
using Microsoft.AspNetCore.Components.Authorization;

namespace K7.Clients.MAUI.Services.Authentication;

public class AuthenticationDelegatingHandler : DelegatingHandler
{
    private readonly IServiceProvider _serviceProvider;
    private int _refreshing;
    private int _logoutTriggered;

    public AuthenticationDelegatingHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Pre-set the auth token before sending (avoids 401 race on concurrent requests)
        if (request.Headers.Authorization is null)
        {
            var deviceStorage = _serviceProvider.GetRequiredService<IDeviceStorageService>();
            var token = deviceStorage.Get(PreferenceKeys.ACCESS_TOKEN);
            if (string.IsNullOrEmpty(token))
            {
                var authProvider = _serviceProvider.GetRequiredService<AuthenticationStateProvider>();
                await authProvider.GetAuthenticationStateAsync();
                token = deviceStorage.Get(PreferenceKeys.ACCESS_TOKEN);
            }

            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode is not HttpStatusCode.Unauthorized)
            return response;

        // Only one thread attempts the refresh
        if (Interlocked.CompareExchange(ref _refreshing, 1, 0) != 0)
            return response;

        try
        {
            var authProvider = _serviceProvider.GetRequiredService<ICustomAuthenticationStateProvider>();

            if (await authProvider.TryRefreshAsync(cancellationToken))
            {
                // Retry the original request with the new access token
                var deviceStorage = _serviceProvider.GetRequiredService<IDeviceStorageService>();
                var newToken = deviceStorage.Get(PreferenceKeys.ACCESS_TOKEN);

                if (!string.IsNullOrEmpty(newToken))
                {
                    var retry = await CloneAndRetryAsync(request, newToken, cancellationToken);
                    if (retry.IsSuccessStatusCode)
                    {
                        response.Dispose();
                        return retry;
                    }

                    retry.Dispose();
                }
            }

            // Refresh failed or retry still 401 -- logout
            if (Interlocked.CompareExchange(ref _logoutTriggered, 1, 0) == 0)
            {
                var authProvider2 = _serviceProvider.GetRequiredService<ICustomAuthenticationStateProvider>();
                await authProvider2.LogoutAsync(cancellationToken);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }

        return response;
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
            if (original.Content.Headers.ContentType is not null)
                newContent.Headers.ContentType = original.Content.Headers.ContentType;
            clone.Content = newContent;
        }

        return await base.SendAsync(clone, cancellationToken);
    }
}
