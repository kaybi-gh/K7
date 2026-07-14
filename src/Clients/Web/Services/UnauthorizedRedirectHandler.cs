using System.Net;
using K7.Clients.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Clients.Web.Services;

public class UnauthorizedRedirectHandler : DelegatingHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly UnauthorizedRedirectGate _redirectGate;
    private readonly ILogger<UnauthorizedRedirectHandler> _logger;

    public UnauthorizedRedirectHandler(
        IServiceProvider serviceProvider,
        UnauthorizedRedirectGate redirectGate,
        ILogger<UnauthorizedRedirectHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _redirectGate = redirectGate;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode is not HttpStatusCode.Unauthorized)
            return response;

        if (!_redirectGate.TryEnter())
            return response;

        try
        {
            var authProvider = _serviceProvider.GetRequiredService<ICustomAuthenticationStateProvider>();
            await authProvider.LoginAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to redirect after unauthorized API response");
        }
        finally
        {
            _redirectGate.Exit();
        }

        return response;
    }
}
