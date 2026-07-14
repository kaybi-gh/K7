namespace K7.Server.Application.Common.Services;

public static class ContinueWatchingEpisodeOrder
{
    // Season 0 (specials) sorts after regular seasons. Cap far above realistic season counts
    // without overflowing int when multiplied by 10_000.
    private const int SpecialsSeasonSortIndex = 100_000;

    public static int GetSortKey(int? seasonNumber, int episodeNumber)
    {
        var season = seasonNumber == 0 ? SpecialsSeasonSortIndex : seasonNumber ?? 0;
        return season * 10_000 + episodeNumber;
    }
}
