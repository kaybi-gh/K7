using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Mappings;

public static class LibraryItemBrowseMappings
{
    public static MediaCardViewModel ToCardViewModel(
        this LitePlaylistDto playlist,
        IK7ServerService apiClient,
        string subtitle) => new()
    {
        Id = playlist.Id.ToString(),
        Kind = MediaCardKind.Cover,
        MediaType = playlist.MediaType,
        Title = playlist.Title,
        AdditionalInformations = subtitle,
        PictureUrl = ResolvePictureUrl(playlist.CoverPicture, playlist.PreviewPictures, apiClient)
    };

    public static MediaCardViewModel ToCardViewModel(
        this LiteCollectionDto collection,
        IK7ServerService apiClient,
        string subtitle) => new()
    {
        Id = collection.Id.ToString(),
        Kind = MediaCardKind.Cover,
        Title = collection.Title,
        AdditionalInformations = subtitle,
        PictureUrl = ResolvePictureUrl(collection.CoverPicture, collection.PreviewPictures, apiClient)
    };

    public static bool UseMosaicCover(MetadataPictureDto? coverPicture, IReadOnlyList<MetadataPictureDto> previewPictures) =>
        coverPicture is null && previewPictures.Count > 0;

    private static string? ResolvePictureUrl(
        MetadataPictureDto? coverPicture,
        IReadOnlyList<MetadataPictureDto> previewPictures,
        IK7ServerService apiClient)
    {
        if (UseMosaicCover(coverPicture, previewPictures))
            return null;

        var picture = coverPicture ?? previewPictures.FirstOrDefault();
        return apiClient.GetAbsoluteUri(picture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
    }
}
