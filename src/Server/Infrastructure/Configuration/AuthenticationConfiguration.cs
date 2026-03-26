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
    public string Scopes { get; set; } = "openid,profile";
}
