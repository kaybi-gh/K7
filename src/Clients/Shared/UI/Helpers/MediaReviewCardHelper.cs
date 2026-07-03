using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Clients.Shared.UI.Helpers;

public static class MediaReviewCardHelper
{
    private const string PeerSuffixSeparator = " @ ";

    public static (string DisplayName, string? PeerName) ParseFederatedDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return ("", null);

        var idx = displayName.LastIndexOf(PeerSuffixSeparator, StringComparison.Ordinal);
        if (idx < 0)
            return (displayName, null);

        return (displayName[..idx], displayName[(idx + PeerSuffixSeparator.Length)..]);
    }

    public static string? GetAvatarUrl(Guid? avatarPictureId) =>
        avatarPictureId is Guid id ? $"/api/metadata-pictures/{id}?size=Medium" : null;

    public static string? GetMediaCoverUrl(Guid? coverPictureId) =>
        coverPictureId is Guid id ? $"/api/metadata-pictures/{id}?size=Small" : null;

    public static MediaCardViewModel ToMediaCardViewModel(SocialUserReviewViewDto review, string untitled)
    {
        var mediaType = review.Media.Media.Type;

        return new MediaCardViewModel
        {
            Id = review.Media.LocalMediaId?.ToString() ?? review.Id.ToString(),
            Kind = GetMediaCardKind(mediaType),
            MediaType = mediaType,
            Title = review.Media.Media.Title ?? untitled,
            PictureUrl = GetMediaCoverUrl(review.Media.CoverPictureId)
        };
    }

    public static MediaCardVariant GetMediaCardVariant(MediaType mediaType) =>
        mediaType switch
        {
            MediaType.MusicTrack or MediaType.MusicAlbum or MediaType.MusicArtist => MediaCardVariant.Cover,
            MediaType.SerieEpisode => MediaCardVariant.Backdrop,
            _ => MediaCardVariant.Poster
        };

    private static MediaCardKind GetMediaCardKind(MediaType mediaType) =>
        mediaType switch
        {
            MediaType.MusicTrack or MediaType.MusicAlbum or MediaType.MusicArtist => MediaCardKind.Cover,
            MediaType.Serie => MediaCardKind.Serie,
            MediaType.SerieSeason => MediaCardKind.Season,
            MediaType.SerieEpisode => MediaCardKind.Episode,
            _ => MediaCardKind.Poster
        };
}
