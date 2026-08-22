using System.Text.RegularExpressions;
using K7.Import.Models;
using K7.Shared.Parsing;

namespace K7.Import.Matching;

/// <summary>
/// Fills series/season/episode from the file path when the source typed the item as generic
/// "video". Combined files (S07E025-E026) keep the source episode index when it falls inside
/// the range (Plex Partie 2 = E26) so a real E26 can match. If that later episode does not
/// exist, import leaves Partie 2 unmatched rather than doubling a watch on E25.
/// </summary>
internal static partial class EpisodeIdentityParser
{
    public static SourceMediaItem Enrich(SourceMediaItem item)
    {
        var mediaType = item.MediaType;
        var seriesTitle = item.SeriesTitle;
        var season = item.SeasonNumber;
        var episode = item.EpisodeNumber;
        var episodeEnd = item.EpisodeNumberEnd;
        var rangeStart = item.EpisodeRangeStart;

        foreach (var path in item.FilePaths)
        {
            if (!TryParseFromPath(path, out var parsedSeries, out var parsedSeason, out var firstEpisode, out var lastEpisode))
                continue;

            seriesTitle ??= parsedSeries;
            season ??= parsedSeason;
            if (lastEpisode > firstEpisode)
            {
                rangeStart = firstEpisode;
                episodeEnd = lastEpisode;
                if (item.EpisodeNumber is int sourceEp
                    && sourceEp >= firstEpisode
                    && sourceEp <= lastEpisode)
                {
                    episode = sourceEp;
                }
                else
                {
                    episode ??= firstEpisode;
                }
            }
            else
            {
                episode ??= firstEpisode;
            }
        }

        if (mediaType is "video" or null
            && season is not null
            && episode is not null
            && !string.IsNullOrWhiteSpace(seriesTitle))
        {
            mediaType = "episode";
        }

        if (mediaType == item.MediaType
            && seriesTitle == item.SeriesTitle
            && season == item.SeasonNumber
            && episode == item.EpisodeNumber
            && episodeEnd == item.EpisodeNumberEnd
            && rangeStart == item.EpisodeRangeStart)
        {
            return item;
        }

        return item with
        {
            MediaType = mediaType,
            SeriesTitle = seriesTitle,
            SeasonNumber = season,
            EpisodeNumber = episode,
            EpisodeNumberEnd = episodeEnd,
            EpisodeRangeStart = rangeStart
        };
    }

    internal static bool TryParseFromPath(
        string path,
        out string? seriesTitle,
        out int? season,
        out int? episode,
        out int lastEpisode)
    {
        seriesTitle = null;
        season = null;
        episode = null;
        lastEpisode = 0;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalized = path.Replace('\\', '/');
        var fileName = Path.GetFileName(normalized);
        if (TryParseSeasonEpisode(fileName, out var parsedSeason, out var firstEpisode, out var parsedLast))
        {
            season = parsedSeason;
            episode = firstEpisode;
            lastEpisode = parsedLast;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            if (!TryParseSeasonFolder(segments[i], out var folderSeason))
                continue;

            season ??= folderSeason;
            if (i > 0)
                seriesTitle = segments[i - 1];
            break;
        }

        return seriesTitle is not null || season is not null || episode is not null;
    }

    private static bool TryParseSeasonFolder(string segment, out int season)
    {
        season = 0;
        var match = SeasonFolderRegex().Match(segment);
        if (!match.Success)
            return false;

        season = int.Parse(match.Groups["n"].Value);
        return season > 0;
    }

    private static bool TryParseSeasonEpisode(string text, out int season, out int firstEpisode, out int lastEpisode)
    {
        season = 0;
        firstEpisode = 0;
        lastEpisode = 0;
        if (!EpisodeRangeParser.TryParse(text, out var parsed) || parsed.Season <= 0 || parsed.FirstEpisode <= 0)
            return false;

        season = parsed.Season;
        firstEpisode = parsed.FirstEpisode;
        lastEpisode = parsed.LastEpisode;
        return true;
    }

    [GeneratedRegex(@"^(?:Season|Saison|Series)\s*(?<n>\d{1,2})$|^S(?<n>\d{1,2})$|(?:Season|Saison|Series|S)(?<n>\d{1,2})$", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonFolderRegex();
}
