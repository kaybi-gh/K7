using System.Net;
using K7.Clients.Shared.Interfaces;

namespace K7.Clients.MAUI.Services.Authentication;

public class AuthenticationDelegatingHandler : DelegatingHandler
{
    private readonly IServiceProvider _serviceProvider;
    private int _logoutTriggered;

    public AuthenticationDelegatingHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized
            && Interlocked.CompareExchange(ref _logoutTriggered, 1, 0) == 0)
        {
            var authProvider = _serviceProvider.GetRequiredService<ICustomAuthenticationStateProvider>();
            await authProvider.LogoutAsync(cancellationToken);
        }

        return response;
    }
}
