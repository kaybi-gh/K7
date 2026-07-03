using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Navigation;

namespace K7.Clients.Shared.UI.Helpers;

public static class SocialUserNavigation
{
    public static string GetProfileHref(SocialUserIdentityDto identity) =>
        identity.IsFederated && identity.PeerServerId is Guid peerId && identity.OriginUserId is Guid originUserId
            ? $"/federation/peers/{peerId}/users/{originUserId}"
            : identity.LocalUserId is Guid userId
                ? $"/users/{userId}"
                : "/users";

    public static string? GetMediaHref(Guid? localMediaId, MediaType? type) =>
        localMediaId is Guid mediaId && type is MediaType mediaType
            ? MediaPageUrls.Build(mediaType, mediaId)
            : null;

    public static string? GetPlaybackHref(SocialUserPlaybackViewDto entry) =>
        entry.MediaHref ?? GetMediaHref(entry.LocalMediaId, entry.MediaType);
}
