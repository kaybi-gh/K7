using K7.Clients.Shared.Models;
using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Mappings;

public static class SocialUserBrowseMappings
{
    public static MediaCardViewModel ToCardViewModel(this SocialUserCollectionCardDto collection, string subtitle) =>
        new()
        {
            Id = collection.Id.ToString(),
            Kind = MediaCardKind.Cover,
            Title = collection.Title,
            AdditionalInformations = subtitle
        };

    public static MediaCardViewModel ToCardViewModel(this SocialUserPlaylistCardDto playlist, string subtitle) =>
        new()
        {
            Id = playlist.Id.ToString(),
            Kind = MediaCardKind.Cover,
            MediaType = playlist.MediaType,
            Title = playlist.Title,
            AdditionalInformations = subtitle
        };

    public static bool UseMosaicCover(Guid? coverPictureId, IReadOnlyList<SocialUserMediaCardDto> previewItems) =>
        coverPictureId is null && previewItems.Count > 0;

    public static IReadOnlyList<string> GetPreviewImageUrls(
        IReadOnlyList<SocialUserMediaCardDto> previewItems,
        IK7ServerService apiClient) =>
        previewItems
            .Select(item => item.CoverPictureId)
            .Where(id => id is not null)
            .Select(id => apiClient.GetAbsoluteUri($"/api/metadata-pictures/{id}?size=Medium")?.AbsoluteUri)
            .Where(url => url is not null)
            .Cast<string>()
            .Take(4)
            .ToList();
}
