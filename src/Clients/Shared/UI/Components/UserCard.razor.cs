using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class UserCard
{
    [Parameter, EditorRequired] public SocialUserIdentityDto Identity { get; set; } = default!;
    [Parameter] public string? Href { get; set; }

    private string ProfileHref => Href ?? SocialUserNavigation.GetProfileHref(Identity);

    private string? AvatarUrl => MediaReviewCardHelper.GetAvatarUrl(Identity.AvatarPictureId);

    private Guid? AvatarUserId => Identity.LocalUserId ?? Identity.OriginUserId;

    private string AvatarInitial =>
        Identity.DisplayName.Length > 0
            ? char.ToUpperInvariant(Identity.DisplayName[0]).ToString()
            : "";

    private string? PeerSubtitle =>
        Identity.IsFederated && !string.IsNullOrWhiteSpace(Identity.PeerName)
            ? Identity.PeerName
            : null;
}
