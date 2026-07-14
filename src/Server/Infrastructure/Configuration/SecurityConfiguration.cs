namespace K7.Server.Infrastructure.Configuration;

public sealed class SecurityConfiguration
{
    public bool ForceHttps { get; set; } = true;
    public string[] KnownProxies { get; set; } = [];
    public FederationSecurityConfiguration Federation { get; set; } = new();
}

public sealed class FederationSecurityConfiguration
{
    public bool AllowInsecurePeerHttp { get; set; }
    public bool BlockPrivatePeerUrls { get; set; }
}
