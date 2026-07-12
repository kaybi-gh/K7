using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace K7.Server.Infrastructure.Database.Context.Services;

public sealed class PeerUrlGuard(IHostEnvironment environment, IOptions<SecurityConfiguration> securityConfiguration) : IPeerUrlGuard
{
    public void EnsureAllowedOutgoingUrl(string url)
    {
        var federation = securityConfiguration.Value.Federation;
        PeerUrlValidator.ValidateOutgoingUrl(url, new PeerUrlValidationOptions
        {
            AllowInsecureHttp = federation.AllowInsecurePeerHttp || environment.IsDevelopment(),
            BlockPrivateNetworks = federation.BlockPrivatePeerUrls
        });
    }
}
