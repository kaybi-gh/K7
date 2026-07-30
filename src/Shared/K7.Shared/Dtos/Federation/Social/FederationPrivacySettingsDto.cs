using System.Text.Json.Serialization;
using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Federation.Social;

public sealed record FederationPrivacySettingsDto
{
    public FederationContentVisibilityDto Share { get; set; } = new();
    public FederationContentVisibilityDto View { get; set; } = new();
}

public sealed record FederationContentVisibilityDto
{
    public VisibilityScope Reviews { get; set; } = VisibilityScope.Nobody;
    public VisibilityScope Collections { get; set; } = VisibilityScope.Nobody;
    public VisibilityScope Playlists { get; set; } = VisibilityScope.Nobody;
    // Wire name kept for stored privacy JSON and federation peers.
    [JsonPropertyName("smartPlaylists")]
    public VisibilityScope DynamicPlaylists { get; set; } = VisibilityScope.Nobody;
    public VisibilityScope PlaybackHistory { get; set; } = VisibilityScope.Nobody;
    public IReadOnlyList<FederationVisibilityGrantDto> Grants { get; set; } = [];
}

public sealed record FederationVisibilityGrantDto
{
    public FederationContentType? ContentType { get; set; }
    public Guid? TargetUserId { get; set; }
    public Guid? TargetPeerServerId { get; set; }
    public Guid? TargetOriginUserId { get; set; }
}
