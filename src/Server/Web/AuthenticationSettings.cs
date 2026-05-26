using K7.Server.Application.Common.Interfaces;
using K7.Server.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace K7.Server.Web;

public class AuthenticationSettings : IAuthenticationSettings
{
    private readonly AuthenticationConfiguration _config;

    public AuthenticationSettings(IOptions<AuthenticationConfiguration> config)
    {
        _config = config.Value;
    }

    public bool LocalSignInEnabled => _config.Local.SignInEnabled;
    public bool OidcEnabled => _config.Oidc.Enabled;
    public string? OidcDisplayName => _config.Oidc.Enabled ? _config.Oidc.DisplayName : null;
}
