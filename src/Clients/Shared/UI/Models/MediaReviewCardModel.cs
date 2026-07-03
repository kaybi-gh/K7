using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos.Entities.Reviews;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Clients.Shared.UI.Models;

public sealed record MediaReviewCardModel
{
    public required Guid Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public string? DisplayName { get; init; }
    public string? ProfileHref { get; init; }
    public Guid? UserId { get; init; }
    public string? AvatarUrl { get; init; }
    public bool IsFederated { get; init; }
    public string? PeerName { get; init; }
    public double Rating { get; init; }
    public string? Text { get; init; }
    public string? Emoji { get; init; }
    public Guid? MediaId { get; init; }

    public static MediaReviewCardModel FromLocal(MediaReviewDto review) => new()
    {
        Id = review.Id,
        Created = review.Created,
        DisplayName = review.UserDisplayName,
        ProfileHref = $"/users/{review.UserId}",
        UserId = review.UserId,
        MediaId = review.MediaId,
        Rating = review.Rating,
        Text = review.Text,
        Emoji = review.Emoji
    };

    public static MediaReviewCardModel FromFederated(FederatedReviewDto review)
    {
        var (authorName, peerName) = MediaReviewCardHelper.ParseFederatedDisplayName(review.Author.DisplayName);
        var profileHref = review.Author.OriginPeerServerId is Guid peerId
            ? $"/federation/peers/{peerId}/users/{review.Author.OriginUserId}"
            : null;

        return new MediaReviewCardModel
        {
            Id = review.Id,
            Created = review.Created,
            DisplayName = authorName,
            ProfileHref = profileHref,
            UserId = review.Author.OriginUserId,
            IsFederated = true,
            PeerName = peerName,
            MediaId = review.Media.RemoteMediaId,
            Rating = review.Rating,
            Text = review.Text,
            Emoji = review.Emoji
        };
    }

    public static MediaReviewCardModel FromProfileReview(
        SocialUserReviewViewDto review,
        SocialUserIdentityDto identity)
    {
        var profileHref = SocialUserNavigation.GetProfileHref(identity);

        return new MediaReviewCardModel
        {
            Id = review.Id,
            Created = review.Created,
            DisplayName = identity.DisplayName,
            ProfileHref = profileHref,
            UserId = identity.LocalUserId,
            AvatarUrl = MediaReviewCardHelper.GetAvatarUrl(identity.AvatarPictureId),
            IsFederated = identity.IsFederated,
            PeerName = identity.PeerName,
            MediaId = review.Media.LocalMediaId,
            Rating = review.Rating,
            Text = review.Text,
            Emoji = review.Emoji
        };
    }
}
