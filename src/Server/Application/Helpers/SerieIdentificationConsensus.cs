using K7.Server.Domain.Entities;
using K7.Server.Domain.Models;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Unifies parsed series titles within a directory when filenames are close variants,
/// so siblings stay in the same CreateMedia batch.
/// </summary>
public static class SerieIdentificationConsensus
{
    private const double DominantClusterRatio = 0.8;

    public static void ApplyDirectoryTitleConsensus(IReadOnlyList<IndexedFile> filesInDirectory)
    {
        var identifications = filesInDirectory
            .Select(f => f.Identification)
            .Where(i => i is not null)
            .Cast<MediaIdentification>()
            .ToList();

        ApplyTitleConsensus(identifications);
    }

    public static void ApplyTitleConsensus(IReadOnlyList<MediaIdentification> identifications)
    {
        var identified = identifications
            .Where(i => !string.IsNullOrWhiteSpace(i.SeriesTitle))
            .ToList();

        if (identified.Count < 2)
            return;

        var titles = identified.Select(i => i.SeriesTitle!).ToList();
        var canonical = ResolveCanonicalSeriesTitle(titles);
        if (canonical is null)
            return;

        foreach (var identification in identified)
        {
            var previousSeriesTitle = identification.SeriesTitle;
            identification.SeriesTitle = canonical;
            if (string.Equals(identification.Title, previousSeriesTitle, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(identification.Title))
            {
                identification.Title = canonical;
            }
        }
    }

    public static string? ResolveCanonicalSeriesTitle(IReadOnlyList<string> titles)
    {
        if (titles.Count == 0)
            return null;

        if (titles.Count == 1)
            return titles[0];

        var clusters = new List<List<string>>();
        foreach (var title in titles)
        {
            var cluster = clusters.FirstOrDefault(c => AreSeriesTitlesClose(c[0], title));
            if (cluster is null)
                clusters.Add([title]);
            else
                cluster.Add(title);
        }

        if (clusters.Count == 1)
            return PickCanonicalTitle(clusters[0]);

        var ordered = clusters.OrderByDescending(c => c.Count).ToList();
        var total = titles.Count;
        var dominant = ordered[0];
        var minDominantCount = Math.Max(2, (int)Math.Ceiling(total * DominantClusterRatio));
        if (dominant.Count >= minDominantCount)
            return PickCanonicalTitle(dominant);

        return null;
    }

    public static bool AreSeriesTitlesClose(string left, string right)
    {
        var a = NormalizeTitle(left);
        var b = NormalizeTitle(right);
        if (a.Length == 0 || b.Length == 0)
            return false;

        if (string.Equals(a, b, StringComparison.Ordinal))
            return true;

        if (a.Length >= 3 && b.Length >= 3 && (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal)))
            return true;

        var maxLen = Math.Max(a.Length, b.Length);
        var distance = LevenshteinDistance(a, b);
        return distance <= Math.Max(1, maxLen / 5);
    }

    private static string PickCanonicalTitle(IReadOnlyList<string> titles) =>
        titles
            .GroupBy(t => t, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key.Length)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .First()
            .Key;

    private static string NormalizeTitle(string title)
    {
        var collapsed = string.Join(' ', title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Trim().ToLowerInvariant();
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
