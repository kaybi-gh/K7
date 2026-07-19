namespace K7.Server.Infrastructure.Configuration;

public class AuthenticationConfiguration
{
    public LocalAuthenticationConfiguration Local { get; set; } = new();
    public OidcAuthenticationConfiguration Oidc { get; set; } = new();
}

public class LocalAuthenticationConfiguration
{
    public bool SignInEnabled { get; set; } = true;
    public bool RegistrationEnabled { get; set; } = true;
}

public class OidcAuthenticationConfiguration
{
    public bool Enabled { get; set; }
    public bool AutomaticAccountCreation { get; set; } = true;
    public string DisplayName { get; set; } = "Oidc";
    public string Authority { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// Comma-separated scopes requested from the IdP (typically openid,profile).
    /// </summary>
    public string Scopes { get; set; } = "openid,profile";

    /// <summary>
    /// Optional. Persistent Identity cookie lifetime for web OIDC sign-ins (default 7 days).
    /// Web only; MAUI uses K7 refresh tokens. Independent of IdP token validity. Override
    /// when a different web session length is desired.
    /// </summary>
    public TimeSpan WebSessionLifetime { get; set; } = TimeSpan.FromDays(7);
}
