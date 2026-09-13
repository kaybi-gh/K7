namespace K7.Shared.Dtos;

public sealed record ServerFeatureFlagsDto
{
    public bool FederationEnabled { get; init; }
    public bool FederationInvitationsEnabled { get; init; } = true;
    public bool MediaLinkPreviewsEnabled { get; init; } = true;
}
