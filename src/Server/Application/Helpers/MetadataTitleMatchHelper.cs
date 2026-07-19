namespace K7.Server.Application.Helpers;

/// <summary>
/// Ranks metadata search candidates so exact title matches beat fuzzy / popularity order.
/// </summary>
public static class MetadataTitleMatchHelper
{
    private const int ExactMatchScore = 10_000;
    private const int StartsWithScore = 8_000;
    private const int WholeWordScore = 6_000;
    private const int ContainedScore = 3_000;
    private const int YearBonus = 500;
    private const int YearMismatchPenalty = 2_000;

    public static int Score(string? query, string? title, int? queryYear = null, int? resultYear = null)
    {
        var normalizedQuery = Normalize(query);
        var normalizedTitle = Normalize(title);
        if (normalizedQuery.Length == 0 || normalizedTitle.Length == 0)
            return 0;

        var score = ScoreNormalizedTitles(normalizedQuery, normalizedTitle);

        if (queryYear.HasValue && resultYear.HasValue)
        {
            if (queryYear.Value == resultYear.Value)
                score += YearBonus;
            else
                score -= YearMismatchPenalty;
        }

        return score;
    }

    public static int Score(
        string? query,
        int? queryYear,
        string? primaryTitle,
        int? resultYear,
        params string?[] alternateTitles)
    {
        var best = Score(query, primaryTitle, queryYear, resultYear);
        foreach (var alternate in alternateTitles)
        {
            var alternateScore = Score(query, alternate, queryYear, resultYear);
            if (alternateScore > best)
                best = alternateScore;
        }

        return best;
    }

    public static T? PickBest<T>(
        string? query,
        int? queryYear,
        IEnumerable<T> candidates,
        Func<T, string?> titleSelector,
        Func<T, int?> yearSelector,
        Func<T, IEnumerable<string?>>? alternateTitlesSelector = null)
    {
        T? best = default;
        var bestScore = int.MinValue;
        var bestTitleLength = int.MaxValue;
        var index = 0;
        var bestIndex = int.MaxValue;
        var hasCandidate = false;

        foreach (var candidate in candidates)
        {
            hasCandidate = true;
            var title = titleSelector(candidate);
            var year = yearSelector(candidate);
            var alternates = alternateTitlesSelector?.Invoke(candidate)?.ToArray() ?? [];
            var score = Score(query, queryYear, title, year, alternates);
            var titleLength = Normalize(title).Length;
            if (titleLength == 0 && alternates.Length > 0)
                titleLength = alternates.Select(Normalize).Where(t => t.Length > 0).Select(t => t.Length).DefaultIfEmpty(int.MaxValue).Min();

            if (score > bestScore
                || (score == bestScore && titleLength < bestTitleLength)
                || (score == bestScore && titleLength == bestTitleLength && index < bestIndex))
            {
                best = candidate;
                bestScore = score;
                bestTitleLength = titleLength;
                bestIndex = index;
            }

            index++;
        }

        return hasCandidate ? best : default;
    }

    public static IReadOnlyList<T> OrderByBestMatch<T>(
        string? query,
        int? queryYear,
        IEnumerable<T> candidates,
        Func<T, string?> titleSelector,
        Func<T, int?> yearSelector,
        Func<T, IEnumerable<string?>>? alternateTitlesSelector = null)
    {
        return candidates
            .Select((candidate, index) =>
            {
                var title = titleSelector(candidate);
                var year = yearSelector(candidate);
                var alternates = alternateTitlesSelector?.Invoke(candidate)?.ToArray() ?? [];
                var score = Score(query, queryYear, title, year, alternates);
                var titleLength = Normalize(title).Length;
                if (titleLength == 0 && alternates.Length > 0)
                {
                    titleLength = alternates
                        .Select(Normalize)
                        .Where(t => t.Length > 0)
                        .Select(t => t.Length)
                        .DefaultIfEmpty(int.MaxValue)
                        .Min();
                }

                return (candidate, score, titleLength, index);
            })
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.titleLength)
            .ThenBy(x => x.index)
            .Select(x => x.candidate)
            .ToList();
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new System.Text.StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (pendingSpace && builder.Length > 0)
                    builder.Append(' ');
                builder.Append(char.ToLowerInvariant(ch));
                pendingSpace = false;
            }
            else if (char.IsWhiteSpace(ch))
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }

    private static int ScoreNormalizedTitles(string query, string title)
    {
        if (string.Equals(query, title, StringComparison.Ordinal))
            return ExactMatchScore;

        if (title.StartsWith(query, StringComparison.Ordinal)
            && (title.Length == query.Length || title[query.Length] == ' '))
            return StartsWithScore;

        if (ContainsWholeWord(title, query))
            return WholeWordScore;

        if (title.Contains(query, StringComparison.Ordinal))
            return ContainedScore;

        var maxLen = Math.Max(query.Length, title.Length);
        if (maxLen == 0)
            return 0;

        var distance = LevenshteinDistance(query, title);
        var similarity = 1.0 - (double)distance / maxLen;
        if (similarity < 0.6)
            return 0;

        return (int)(similarity * ContainedScore);
    }

    private static bool ContainsWholeWord(string title, string query)
    {
        var start = 0;
        while (start <= title.Length - query.Length)
        {
            var index = title.IndexOf(query, start, StringComparison.Ordinal);
            if (index < 0)
                return false;

            var beforeOk = index == 0 || title[index - 1] == ' ';
            var afterIndex = index + query.Length;
            var afterOk = afterIndex == title.Length || title[afterIndex] == ' ';
            if (beforeOk && afterOk)
                return true;

            start = index + 1;
        }

        return false;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var n = a.Length;
        var m = b.Length;
        var prev = new int[m + 1];
        var curr = new int[m + 1];

        for (var j = 0; j <= m; j++)
            prev[j] = j;

        for (var i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= m; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }

            (prev, curr) = (curr, prev);
        }

        return prev[m];
    }
}
