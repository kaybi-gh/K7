using System.Net;
using K7.Clients.Shared.Interfaces;

namespace K7.Clients.Web.Services;

public class UnauthorizedRedirectHandler : DelegatingHandler
{
    private readonly IServiceProvider _serviceProvider;
    private int _redirecting;

    public UnauthorizedRedirectHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode is not HttpStatusCode.Unauthorized)
            return response;

        // Only one concurrent call triggers the redirect
        if (Interlocked.CompareExchange(ref _redirecting, 1, 0) != 0)
            return response;

        try
        {
            var authProvider = _serviceProvider.GetRequiredService<ICustomAuthenticationStateProvider>();
            await authProvider.LoginAsync(cancellationToken);
        }
        catch
        {
            Interlocked.Exchange(ref _redirecting, 0);
        }

        return response;
    }
}
