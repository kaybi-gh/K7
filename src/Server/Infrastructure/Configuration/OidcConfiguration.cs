namespace K7.Server.Infrastructure.Configuration;

public class OidcConfiguration
{
    public bool Enabled { get; set; } = false;
    public string DisplayName { get; set; } = "Oidc";
    public string Authority { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Scopes { get; set; } = "openid,profile";
}
