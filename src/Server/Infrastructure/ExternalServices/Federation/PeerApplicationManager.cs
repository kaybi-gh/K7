using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using OpenIddict.Abstractions;

namespace K7.Server.Infrastructure.ExternalServices.Federation;

public class PeerApplicationManager(IOpenIddictApplicationManager applicationManager) : IPeerApplicationManager
{
    public async Task<string> CreatePeerApplicationAsync(string clientId, string clientSecret, string displayName, CancellationToken cancellationToken = default)
    {
        var application = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            DisplayName = displayName,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Prefixes.Scope + FederationScopes.Peer
            }
        };

        var result = await applicationManager.CreateAsync(application, cancellationToken);
        return (await applicationManager.GetIdAsync(result, cancellationToken))!;
    }

    public async Task DeletePeerApplicationAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var app = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (app is not null)
            await applicationManager.DeleteAsync(app, cancellationToken);
    }
}
