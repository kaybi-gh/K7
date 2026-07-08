namespace K7.Server.Application.Common.Services;

public static class ContinueWatchingEpisodeOrder
{
    public static int GetSortKey(int? seasonNumber, int episodeNumber)
    {
        var season = seasonNumber == 0 ? int.MaxValue : seasonNumber ?? 0;
        return season * 10_000 + episodeNumber;
    }
}
