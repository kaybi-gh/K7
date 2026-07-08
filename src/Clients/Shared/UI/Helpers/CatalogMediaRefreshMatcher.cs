using K7.Clients.Shared.Models;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Clients.Shared.UI.Helpers;

internal static class CatalogMediaRefreshMatcher
{
    internal static bool IsCardAffected(IReadOnlyList<MediaCardViewModel> items, Guid mediaId)
    {
        var id = mediaId.ToString();
        return items.Any(item => item.Id == id || item.ParentId == id);
    }

    internal static bool IsLiteMediaAffected(IReadOnlyList<LiteMediaDto> items, Guid mediaId) =>
        items.Any(item => item.Id == mediaId);
}
