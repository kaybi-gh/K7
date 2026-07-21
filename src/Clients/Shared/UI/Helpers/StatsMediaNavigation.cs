using K7.Shared.Dtos;
using K7.Shared.Navigation;

namespace K7.Clients.Shared.UI.Helpers;

public static class StatsMediaNavigation
{
    public static string? GetHref(TopItemDto item) =>
        MediaPageUrls.BuildFromTypeName(
            item.MediaType,
            item.Id,
            serieId: item.ParentId,
            seasonNumber: item.SeasonNumber,
            episodeNumber: item.EpisodeNumber,
            albumId: item.ParentId);
}
