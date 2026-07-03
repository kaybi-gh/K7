using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Federation.Social;

public sealed record FederationContentTypePolicyDto
{
    public bool Outbound { get; set; } = true;
    public bool Inbound { get; set; } = true;
}

public sealed record FederationSocialPolicyDto
{
    public bool Enabled { get; set; }
    public Dictionary<FederationContentType, FederationContentTypePolicyDto> Policies { get; set; } = new();
}
