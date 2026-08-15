using System.Globalization;
using System.Text.RegularExpressions;

namespace K7.Shared.Parsing;

/// <summary>
/// Parses season/episode tokens from a filename or title, including combined-episode ranges.
/// Same-season only. The range is the inclusive min-max of collected episode numbers.
/// Cross-season tokens (S01E01-S02E01) stop the scan; only the first episode is kept.
/// </summary>
public readonly record struct EpisodeRangeParseResult(
    int Season,
    int FirstEpisode,
    int LastEpisode,
    int MatchIndex,
    int MatchLength);

public static partial class EpisodeRangeParser
{
    public static bool TryParse(string? text, out EpisodeRangeParseResult result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = NormalizeRangePunctuation(text);
        if (!TryFindStart(normalized, out var startIndex, out var season, out var firstEpisode, out var startLength))
            return false;

        var pos = startIndex + startLength;
        var min = firstEpisode;
        var max = firstEpisode;

        while (TryConsumeContinuation(normalized, pos, season, out var nextPos, out var episode))
        {
            min = Math.Min(min, episode);
            max = Math.Max(max, episode);
            pos = nextPos;
        }

        result = new EpisodeRangeParseResult(season, min, max, startIndex, startLength);
        return true;
    }

    private static string NormalizeRangePunctuation(string text) =>
        text
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .Replace('\u2212', '-');

    private static bool TryFindStart(
        string text,
        out int index,
        out int season,
        out int episode,
        out int length)
    {
        index = 0;
        season = 0;
        episode = 0;
        length = 0;

        var sxx = SxxExxStart().Match(text);
        var nx = NxNNStart().Match(text);

        Match? chosen = null;
        if (sxx.Success && nx.Success)
            chosen = sxx.Index <= nx.Index ? sxx : nx;
        else if (sxx.Success)
            chosen = sxx;
        else if (nx.Success)
            chosen = nx;

        if (chosen is null)
            return false;

        season = int.Parse(chosen.Groups["season"].Value, CultureInfo.InvariantCulture);
        episode = int.Parse(chosen.Groups["episode"].Value, CultureInfo.InvariantCulture);
        if (season < 0 || episode <= 0)
            return false;

        index = chosen.Index;
        length = chosen.Length;
        return true;
    }

    private static bool TryConsumeContinuation(
        string text,
        int pos,
        int expectedSeason,
        out int newPos,
        out int episode)
    {
        newPos = pos;
        episode = 0;

        var i = pos;
        while (i < text.Length && IsSoftSeparator(text[i]))
            i++;

        var hadRangePunctuation = false;
        if (i < text.Length && IsRangePunctuation(text[i]))
        {
            hadRangePunctuation = true;
            i++;
            while (i < text.Length && IsSoftSeparator(text[i]))
                i++;
        }

        if (i >= text.Length)
            return false;

        var sxx = SxxExxStart().Match(text, i);
        if (sxx.Success && sxx.Index == i)
        {
            var season = int.Parse(sxx.Groups["season"].Value, CultureInfo.InvariantCulture);
            if (season != expectedSeason)
                return false;

            episode = int.Parse(sxx.Groups["episode"].Value, CultureInfo.InvariantCulture);
            if (episode <= 0)
                return false;

            newPos = sxx.Index + sxx.Length;
            return true;
        }

        var nx = NxNNStart().Match(text, i);
        if (nx.Success && nx.Index == i)
        {
            var season = int.Parse(nx.Groups["season"].Value, CultureInfo.InvariantCulture);
            if (season != expectedSeason)
                return false;

            episode = int.Parse(nx.Groups["episode"].Value, CultureInfo.InvariantCulture);
            if (episode <= 0)
                return false;

            newPos = nx.Index + nx.Length;
            return true;
        }

        var episodeOnly = EpisodeOnly().Match(text, i);
        if (episodeOnly.Success && episodeOnly.Index == i)
        {
            episode = int.Parse(episodeOnly.Groups["episode"].Value, CultureInfo.InvariantCulture);
            if (episode <= 0)
                return false;

            newPos = episodeOnly.Index + episodeOnly.Length;
            return true;
        }

        if (!hadRangePunctuation)
            return false;

        var bare = BareNumber().Match(text, i);
        if (!bare.Success || bare.Index != i)
            return false;

        episode = int.Parse(bare.Groups["episode"].Value, CultureInfo.InvariantCulture);
        if (episode <= 0 || IsNoiseNumber(episode))
            return false;

        newPos = bare.Index + bare.Length;
        return true;
    }

    private static bool IsSoftSeparator(char c) => c is '.' or '_' or ' ' or '\t';

    private static bool IsRangePunctuation(char c) => c is '-' or '~' or '+';

    private static bool IsNoiseNumber(int value) =>
        value is 480 or 576 or 720 or 1080 or 1440 or 2160 or 4320
        || value is >= 1900 and <= 2099;

    [GeneratedRegex(@"[Ss](?<season>\d{1,2})\s*[Ee](?<episode>\d{1,4})", RegexOptions.CultureInvariant)]
    private static partial Regex SxxExxStart();

    [GeneratedRegex(@"(?<!\d)(?<season>\d{1,2})[Xx](?<episode>\d{1,3})", RegexOptions.CultureInvariant)]
    private static partial Regex NxNNStart();

    [GeneratedRegex(@"[Ee](?<episode>\d{1,4})", RegexOptions.CultureInvariant)]
    private static partial Regex EpisodeOnly();

    [GeneratedRegex(@"(?<episode>\d{1,4})(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex BareNumber();
}
