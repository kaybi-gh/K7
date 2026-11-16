namespace K7.Shared.Dtos;

public sealed record ServerFeatureFlagsDto
{
    public bool FederationEnabled { get; init; }
    public bool FederationInvitationsEnabled { get; init; } = true;
    public bool MediaLinkPreviewsEnabled { get; init; } = true;

    /// <summary>
    /// Public share links (anonymous access to one media). Off by default: turning it on
    /// exposes the shared media to anyone holding a link.
    /// </summary>
    public bool PublicSharesEnabled { get; init; }
}
