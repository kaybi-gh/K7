using K7.Clients.Shared.Interfaces;
using K7.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Attaches the active shared profile id so the server can scope continue watching / history / prefs.
/// Resolves the session lazily to avoid a DI cycle:
/// handler -> session -> cache -> api -> HttpClient -> handler.
/// </summary>
public sealed class SharedProfileHeaderHandler(IServiceProvider serviceProvider) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var session = serviceProvider.GetService<ISharedProfileSessionService>();
        var profileId = session?.ActiveGroupId;
        if (profileId is { } id)
            request.Headers.TryAddWithoutValidation(HttpHeaderNames.SharedProfileId, id.ToString());

        return base.SendAsync(request, cancellationToken);
    }
}
