namespace K7.Server.Infrastructure.Configuration;

public sealed class SecurityConfiguration
{
    public bool ForceHttps { get; set; } = true;

    /// <summary>
    /// When true and <see cref="KnownProxies"/> is empty, trust RFC1918 / loopback / ULA
    /// for <c>X-Forwarded-Proto</c> (typical Docker + Traefik / Caddy / nginx setups).
    /// </summary>
    public bool TrustPrivateProxies { get; set; } = true;

    public string[] KnownProxies { get; set; } = [];
    public ApiKeysSecurityConfiguration ApiKeys { get; set; } = new();
    public FederationSecurityConfiguration Federation { get; set; } = new();
}

public sealed class ApiKeysSecurityConfiguration
{
    /// <summary>
    /// HMAC secret mixed into API key hashes. Required at startup.
    /// Prefer env/file secret in production. Changing it invalidates all existing API keys.
    /// </summary>
    public string HashSecret { get; set; } = string.Empty;
}

public sealed class FederationSecurityConfiguration
{
    public bool AllowInsecurePeerHttp { get; set; }
    public bool BlockPrivatePeerUrls { get; set; }
}
