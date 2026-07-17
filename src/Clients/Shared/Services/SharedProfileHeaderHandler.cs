using K7.Clients.Shared.Interfaces;
using K7.Shared;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Attaches the active shared profile id so the server can scope continue watching / history / prefs.
/// </summary>
public sealed class SharedProfileHeaderHandler(ISharedProfileSessionService sharedProfileSession) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var profileId = sharedProfileSession.ActiveGroupId;
        if (profileId is { } id)
            request.Headers.TryAddWithoutValidation(HttpHeaderNames.SharedProfileId, id.ToString());

        return base.SendAsync(request, cancellationToken);
    }
}
