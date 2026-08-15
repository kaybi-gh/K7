using System.Text.RegularExpressions;
using K7.Shared.Parsing;

namespace K7.Server.Application.Features.Medias.Services;

public static partial class MediaIdentityKeys
{
    private static readonly string[] TitleArtistSeparators = [" - ", " \u2013 ", " \u2014 "];

    public static string NormalizeMovieTitle(string title, int? year) =>
        year is null ? title : $"{title}|{year.Value}";

    public static string NormalizeSerieTitle(string title, int? year) =>
        NormalizeMovieTitle(title, year);

    public static string NormalizeEpisodeKey(string? seriesTitle, int? seasonNumber, int? episodeNumber, string title) =>
        $"{seriesTitle ?? "Unknown Series"}|S{seasonNumber ?? 0}|E{episodeNumber ?? 0}|{title}";

    /// <summary>
    /// Stable music identity key: "Artist - Title". Strips a redundant artist baked into the
    /// title so "When You Know - Puggy" + Puggy and "When You Know" + Puggy share one key.
    /// </summary>
    public static string NormalizeMusicTitle(string? artistName, string title)
    {
        var (core, artist) = ResolveMusicTitleAndArtist(title, artistName);
        var normalizedArtist = NormalizePersonName(artist);
        return normalizedArtist is not null ? $"{normalizedArtist} - {core}" : core;
    }

    /// <summary>
    /// When the source has no artist, titles like "Efile - KIZ" still carry it after a dash.
    /// </summary>
    public static (string Title, string? ArtistName) ResolveMusicTitleAndArtist(string title, string? artistName)
    {
        if (!string.IsNullOrWhiteSpace(artistName))
            return (StripTrackEditionSuffix(StripRedundantArtistFromTitle(StripFeatureCredits(title), artistName)), artistName);

        if (string.IsNullOrWhiteSpace(title))
            return (title, artistName);

        var trimmed = CollapseWhitespaceRegex().Replace(title.Trim(), " ");
        var separatorIndex = LastTitleArtistSeparatorIndex(trimmed);
        if (separatorIndex <= 0 || separatorIndex + 3 >= trimmed.Length)
            return (StripTrackEditionSuffix(StripFeatureCredits(trimmed)), artistName);

        var left = trimmed[..separatorIndex].Trim();
        var right = trimmed[(separatorIndex + 3)..].Trim();
        if (left.Length == 0 || right.Length == 0 || LastTitleArtistSeparatorIndex(right) >= 0)
            return (StripTrackEditionSuffix(StripFeatureCredits(trimmed)), artistName);

        return (StripTrackEditionSuffix(StripFeatureCredits(left)), right);
    }

    /// <summary>
    /// True when titles/names are the same ignoring case, diacritics, and curly quotes.
    /// Uses the same folding as <see cref="MediaSortTitleHelper.Compute"/>.
    /// </summary>
    public static bool MatchesIgnoringDiacritics(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            return true;

        var leftFolded = FoldLookupPunctuation(left);
        var rightFolded = FoldLookupPunctuation(right);
        if (string.Equals(leftFolded, rightFolded, StringComparison.OrdinalIgnoreCase))
            return true;

        var leftSort = MediaSortTitleHelper.Compute(leftFolded);
        var rightSort = MediaSortTitleHelper.Compute(rightFolded);
        return leftSort is not null
            && rightSort is not null
            && string.Equals(leftSort, rightSort, StringComparison.OrdinalIgnoreCase);
    }

    public static string FoldLookupPunctuation(string value)
    {
        var folded = value
            .Replace('\u2018', '\'')
            .Replace('\u2019', '\'')
            .Replace('\u201B', '\'')
            .Replace('\u2032', '\'')
            .Replace('\u201C', '"')
            .Replace('\u201D', '"')
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .Replace('\u2044', '/')
            .Replace('\u2215', '/')
            .Replace('\uFF0F', '/');

        folded = folded.Replace(" & ", " and ", StringComparison.Ordinal);
        // Subtitle separators: "Show : Subtitle", "Show: Subtitle", "Show, Subtitle".
        folded = CollapseSubtitleSeparatorRegex().Replace(folded, ":");
        folded = folded.Replace('-', ' ').Replace('_', ' ');
        folded = CollapseWhitespaceRegex().Replace(folded, " ").Trim();
        // FR/EN "Face a face" / "Face to Face" (also after "Face-to-Face" -> spaces).
        return RepeatedWordSeparatorRegex().Replace(folded, "${word} a ${word}");
    }

    public static bool IsVariousArtist(string? name)
    {
        var normalized = NormalizePersonName(name);
        if (normalized is null)
            return false;

        return normalized.Equals("Various Artists", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Various", StringComparison.OrdinalIgnoreCase)
            || MatchesIgnoringDiacritics(normalized, "Artistes divers")
            || MatchesIgnoringDiacritics(normalized, "Multi-interpretes");
    }

    public static bool YearsCompatible(int? itemYear, DateOnly? releaseDate)
    {
        if (itemYear is null || releaseDate is null)
            return true;

        return Math.Abs(releaseDate.Value.Year - itemYear.Value) <= 1;
    }

    public static List<string> TitleLookupVariants(params string?[] titles)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var title in titles)
        {
            if (string.IsNullOrWhiteSpace(title))
                continue;

            void Add(string value)
            {
                if (seen.Add(value))
                    result.Add(value);
            }

            Add(title);
            var folded = FoldLookupPunctuation(title);
            Add(folded);
            var sort = MediaSortTitleHelper.Compute(folded);
            if (sort is not null)
                Add(sort);

            // SQL IN compares raw DB titles/sort titles. Emit curly-apostrophe and
            // hyphenated twins so ASCII apostrophes hit curly ones and
            // "Cerf volant" hits "Cerf-volant".
            if (folded.Contains('\''))
            {
                var curly = folded.Replace('\'', '\u2019');
                Add(curly);
                var curlySort = MediaSortTitleHelper.Compute(curly);
                if (curlySort is not null)
                    Add(curlySort);
            }

            if (folded.Contains(' '))
                Add(folded.Replace(' ', '-'));

            if (folded.Contains(':'))
                Add(CollapseWhitespaceRegex().Replace(folded.Replace(':', ' '), " ").Trim());
        }

        return result;
    }

    public static List<string> SeriesTitleLookupVariants(string? title, bool includeYearSuffix = false)
    {
        if (string.IsNullOrWhiteSpace(title))
            return [];

        var trimmed = title.Trim();
        var countryStripped = SeriesCountrySuffixRegex().Replace(trimmed, "").Trim();
        var yearStripped = includeYearSuffix ? StripSeriesYearSuffix(trimmed) : null;
        var countryAndYear = includeYearSuffix && countryStripped.Length > 0
            ? StripSeriesYearSuffix(countryStripped)
            : null;
        var punctStripped = StripSeriesTrailingPunctuation(trimmed);
        var fillerStripped = StripSeriesFillerWords(trimmed);
        var longO = FoldJapaneseLongO(trimmed);
        var sort = MediaSortTitleHelper.Compute(FoldLookupPunctuation(trimmed));
        var fillerFromSort = StripSeriesFillerWords(sort);
        var subtitle = SeriesSubtitle(trimmed);

        return TitleLookupVariants(
            trimmed,
            countryStripped,
            yearStripped,
            countryAndYear,
            punctStripped,
            fillerStripped,
            StripSeriesFillerWords(punctStripped),
            longO,
            fillerFromSort,
            subtitle);
    }

    public static string? StripSeriesTrailingPunctuation(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title;

        var trimmed = title.Trim();
        var stripped = trimmed.TrimEnd('.', '!', '?');
        return stripped.Length == 0 ? trimmed : stripped;
    }

    public static string? StripSeriesFillerWords(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title;

        var stripped = SeriesFillerWordsRegex().Replace(title, " ");
        stripped = CollapseWhitespaceRegex().Replace(stripped, " ").Trim();
        return stripped.Length == 0 ? title.Trim() : stripped;
    }

    /// <summary>
    /// Japanese romanization often drops the extra u (Bungou / Bungo). Only rewrites
    /// token-final "ou" after a consonant in words of 6+ letters so "You" stays intact.
    /// </summary>
    public static string? FoldJapaneseLongO(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title;

        var folded = JapaneseLongORegex().Replace(title, "o");
        return string.Equals(folded, title, StringComparison.Ordinal) ? null : folded;
    }

    /// <summary>
    /// First segment before " - " / " : " when it is long enough to be a show nickname
    /// (Konosuba, DanMachi). Null when the title has no separator.
    /// </summary>
    public static string? SeriesShortName(string? title)
    {
        if (string.IsNullOrWhiteSpace(title) || !TryFindSubtitleSeparator(title.Trim(), out var idx, out _))
            return null;

        var shortName = title.Trim()[..idx].Trim();
        return shortName.Length >= 4 ? shortName : null;
    }

    /// <summary>
    /// Text after the first " - " / " : " / ", " when long enough to be the English
    /// (or localized) subtitle. "Tsugai - Daemons of the Shadow Realm" -> the Daemons part.
    /// Null when there is no separator. Does not emit the leading nickname (DanMachi).
    /// </summary>
    public static string? SeriesSubtitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)
            || !TryFindSubtitleSeparator(title.Trim(), out var idx, out var length))
        {
            return null;
        }

        var subtitle = title.Trim()[(idx + length)..].Trim();
        return subtitle.Length >= 8 ? subtitle : null;
    }

    private static bool TryFindSubtitleSeparator(string title, out int index, out int length)
    {
        index = -1;
        length = 0;
        foreach (var separator in new[] { " - ", " : ", ", ", " \u2013 ", " \u2014 " })
        {
            var found = title.IndexOf(separator, StringComparison.Ordinal);
            if (found > 0 && (index < 0 || found < index))
            {
                index = found;
                length = separator.Length;
            }
        }

        if (index < 0)
        {
            var colon = title.IndexOf(':');
            if (colon > 0)
            {
                index = colon;
                length = 1;
            }
        }

        return index > 0;
    }

    public static List<T> FindSeriesByShortNamePrefix<T>(
        string? queryTitle,
        IReadOnlyList<T> series,
        Func<T, string?> titleSelector,
        Func<T, string?> originalTitleSelector)
    {
        var shortName = SeriesShortName(queryTitle);
        if (shortName is null || series.Count == 0)
            return [];

        var foldedShort = FoldLookupPunctuation(shortName);
        if (foldedShort.Length < 4)
            return [];

        return series
            .Where(s => StartsWithDistinctivePrefix(titleSelector(s), foldedShort)
                || StartsWithDistinctivePrefix(originalTitleSelector(s), foldedShort))
            .ToList();
    }

    public static string? DistinctiveLastToken(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var folded = FoldLookupPunctuation(title);
        string? last = null;
        foreach (var token in folded.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Length < 6 || SeriesStopwordRegex().IsMatch(token))
                continue;

            last = token;
        }

        return last;
    }

    public static List<T> FindSeriesContainingToken<T>(
        string? token,
        IReadOnlyList<T> series,
        Func<T, string?> titleSelector,
        Func<T, string?> originalTitleSelector)
    {
        if (string.IsNullOrWhiteSpace(token) || series.Count == 0)
            return [];

        var foldedToken = FoldLookupPunctuation(token);
        if (foldedToken.Length < 6)
            return [];

        return series
            .Where(s => ContainsDistinctiveToken(titleSelector(s), foldedToken)
                || ContainsDistinctiveToken(originalTitleSelector(s), foldedToken))
            .ToList();
    }

    public static bool AlbumTitlesOverlap(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        var a = FoldLookupPunctuation(StripAlbumEditionSuffix(left) ?? "");
        var b = FoldLookupPunctuation(StripAlbumEditionSuffix(right) ?? "");
        if (a.Length == 0 || b.Length == 0)
            return false;

        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return true;

        const int minPrefixLength = 6;
        if (a.Length < minPrefixLength || b.Length < minPrefixLength)
            return false;

        return a.StartsWith(b, StringComparison.OrdinalIgnoreCase)
            || b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<string> EpisodeTitleSegments(params string?[] titles)
    {
        foreach (var title in titles)
        {
            if (string.IsNullOrWhiteSpace(title))
                continue;

            yield return title;
            foreach (var part in title.Split(" / ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Length >= 8 && !string.Equals(part, title, StringComparison.Ordinal))
                    yield return part;
            }
        }
    }

    public static string? StripSeriesYearSuffix(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title;

        var stripped = SeriesYearSuffixRegex().Replace(title.Trim(), "").Trim();
        return stripped.Length == 0 ? title.Trim() : stripped;
    }

    public static bool SeriesTitlesOverlap(string? left, string? right, bool includeYearSuffix = false)
    {
        foreach (var a in SeriesTitleLookupVariants(left, includeYearSuffix))
        {
            foreach (var b in SeriesTitleLookupVariants(right, includeYearSuffix))
            {
                if (MatchesIgnoringDiacritics(a, b))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Picks series rows that match <paramref name="queryTitle"/>. Exact / country / colon
    /// overlap wins. A trailing (YYYY) is used only when that leaves a single candidate,
    /// so "One Piece" does not bind to both the anime and "One Piece (2023)".
    /// </summary>
    public static List<T> ResolveSeriesMatches<T>(
        string? queryTitle,
        IReadOnlyList<T> series,
        Func<T, string?> titleSelector,
        Func<T, string?> originalTitleSelector)
    {
        if (string.IsNullOrWhiteSpace(queryTitle) || series.Count == 0)
            return [];

        var exact = series
            .Where(s => SeriesTitlesOverlap(queryTitle, titleSelector(s))
                || SeriesTitlesOverlap(queryTitle, originalTitleSelector(s)))
            .ToList();
        if (exact.Count > 0)
            return exact;

        var byYear = series
            .Where(s => SeriesTitlesOverlap(queryTitle, titleSelector(s), includeYearSuffix: true)
                || SeriesTitlesOverlap(queryTitle, originalTitleSelector(s), includeYearSuffix: true))
            .ToList();
        if (byYear.Count == 1)
            return byYear;

        return ResolveSeriesByEditDistance(queryTitle, series, titleSelector, originalTitleSelector);
    }

    public static List<T> ResolveSeriesByEditDistance<T>(
        string? queryTitle,
        IReadOnlyList<T> series,
        Func<T, string?> titleSelector,
        Func<T, string?> originalTitleSelector)
    {
        if (string.IsNullOrWhiteSpace(queryTitle) || series.Count == 0)
            return [];

        var queryFolded = FoldLookupPunctuation(queryTitle);
        if (queryFolded.Length < 10)
            return [];

        var hits = series
            .Where(s => IsEditDistanceAtMostOne(queryFolded, titleSelector(s))
                || IsEditDistanceAtMostOne(queryFolded, originalTitleSelector(s)))
            .ToList();
        return hits.Count == 1 ? hits : [];
    }

    public static bool TryParseSeasonEpisodeRange(string? text, out int season, out int firstEpisode, out int lastEpisode)
    {
        season = 0;
        firstEpisode = 0;
        lastEpisode = 0;
        if (!EpisodeRangeParser.TryParse(text, out var parsed))
            return false;

        season = parsed.Season;
        firstEpisode = parsed.FirstEpisode;
        lastEpisode = parsed.LastEpisode;
        return season > 0 && firstEpisode > 0 && lastEpisode >= firstEpisode;
    }

    public static bool TryParseSeasonEpisode(string? text, out int season, out int episode)
    {
        var parsed = TryParseSeasonEpisodeRange(text, out season, out episode, out _);
        return parsed;
    }

    /// <summary>
    /// Drops edition markers so "Des tours (Deluxe)" matches "Des tours".
    /// Leaves recording variants like (Live) or (Acoustic) alone.
    /// </summary>
    public static string? StripAlbumEditionSuffix(string? albumName)
    {
        if (string.IsNullOrWhiteSpace(albumName))
            return albumName;

        var trimmed = CollapseWhitespaceRegex().Replace(albumName.Trim(), " ");
        var stripped = AlbumEditionSuffixRegex().Replace(trimmed, "").Trim();
        return stripped.Length == 0 ? trimmed : stripped;
    }

    /// <summary>
    /// Drops recording-edition markers so "Song - Original Version" matches "Song".
    /// Leaves live / remix / acoustic / instrumental titles alone.
    /// </summary>
    public static string StripTrackEditionSuffix(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title;

        var trimmed = CollapseWhitespaceRegex().Replace(title.Trim(), " ");
        var stripped = TrackEditionSuffixRegex().Replace(trimmed, "").Trim();
        return stripped.Length == 0 ? trimmed : stripped;
    }

    public static string? NormalizePersonName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var trimmed = CollapseWhitespaceRegex().Replace(name.Trim(), " ");
        // Drop a leading "The " so "The Beatles" matches "Beatles".
        if (trimmed.StartsWith("The ", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 4)
            trimmed = trimmed[4..];

        return trimmed;
    }

    /// <summary>
    /// MusicBrainz person sort names are "Last, First". Unfolds to "First Last".
    /// Leaves album-style sort titles (colon / extra commas) unchanged.
    /// </summary>
    public static string? UnfoldCommaSortName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var trimmed = name.Trim();
        if (trimmed.Contains(':') || trimmed.Contains(';'))
            return trimmed;

        var comma = trimmed.IndexOf(',');
        if (comma <= 0 || comma != trimmed.LastIndexOf(',') || comma + 1 >= trimmed.Length)
            return trimmed;

        var last = trimmed[..comma].Trim();
        var first = trimmed[(comma + 1)..].Trim();
        if (last.Length == 0 || first.Length == 0)
            return trimmed;

        return $"{first} {last}";
    }

    public static bool PersonNamesMatch(string? left, string? right)
    {
        if (MatchesIgnoringDiacritics(left, right))
            return true;

        var leftUnfolded = UnfoldCommaSortName(left);
        var rightUnfolded = UnfoldCommaSortName(right);
        return MatchesIgnoringDiacritics(leftUnfolded, right)
            || MatchesIgnoringDiacritics(left, rightUnfolded)
            || MatchesIgnoringDiacritics(leftUnfolded, rightUnfolded);
    }

    /// <summary>
    /// Removes a redundant artist prefix/suffix from a title ("When You Know - Puggy" / "Puggy - When You Know").
    /// Matching still uses title core + artist separately; this only cleans the title string.
    /// </summary>
    public static string StripRedundantArtistFromTitle(string title, string? artistName)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title;

        var trimmed = CollapseWhitespaceRegex().Replace(title.Trim(), " ");
        var artist = NormalizePersonName(artistName);
        if (artist is null || artist.Length == 0)
            return trimmed;

        foreach (var separator in TitleArtistSeparators)
        {
            var suffix = separator + artist;
            if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && trimmed.Length > suffix.Length)
                trimmed = trimmed[..^suffix.Length].TrimEnd();

            var prefix = artist + separator;
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && trimmed.Length > prefix.Length)
                trimmed = trimmed[prefix.Length..].TrimStart();
        }

        return trimmed;
    }

    public static string NormalizeKey(string part1, string part2) =>
        $"{part1.ToUpperInvariant()}|{part2.ToUpperInvariant()}";

    public static string StripFeatureCredits(string title) =>
        FeatureCreditsRegex().Replace(title, "").Trim();

    private static int LastTitleArtistSeparatorIndex(string title)
    {
        var hyphen = title.LastIndexOf(" - ", StringComparison.Ordinal);
        var enDash = title.LastIndexOf(" \u2013 ", StringComparison.Ordinal);
        var emDash = title.LastIndexOf(" \u2014 ", StringComparison.Ordinal);
        return Math.Max(hyphen, Math.Max(enDash, emDash));
    }

    [GeneratedRegex(@"\s*[\(\[](feat\.?|ft\.?|with)\s.+?[\)\]]", RegexOptions.IgnoreCase)]
    private static partial Regex FeatureCreditsRegex();

    [GeneratedRegex(
        @"\s*[\(\[]((super\s+)?deluxe(\s+edition)?|remaster(ed)?(\s+\d{4})?|expanded(\s+edition)?|special\s+edition|limited\s+edition|bonus\s+tracks?(\s+edition)?|anniversary(\s+edition)?|original(\s+motion\s+picture)?(\s+soundtrack)?|motion\s+picture\s+soundtrack|official\s+soundtrack|soundtrack)[\)\]]",
        RegexOptions.IgnoreCase)]
    private static partial Regex AlbumEditionSuffixRegex();

    [GeneratedRegex(
        @"(?:\s*(?:-|\u2013|\u2014)\s*|\s*[\(\[])(original(\s+version)?|album(\s+version)?|remaster(ed)?(\s+\d{4})?)[\)\]]?\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex TrackEditionSuffixRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespaceRegex();

    [GeneratedRegex(@"\s*[\(\[](?:U\.S\.A\.|USA|U\.S\.|US|U\.K\.|UK|AU|CA|FR|DE)[\)\]]\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SeriesCountrySuffixRegex();

    [GeneratedRegex(@"\s*\(\d{4}\)\s*$")]
    private static partial Regex SeriesYearSuffixRegex();

    [GeneratedRegex(@"\s*[,:]\s*")]
    private static partial Regex CollapseSubtitleSeparatorRegex();

    [GeneratedRegex(@"\b(?<word>\w+)\s+(?:to|a|\u00e0)\s+\k<word>\b", RegexOptions.IgnoreCase)]
    private static partial Regex RepeatedWordSeparatorRegex();

    [GeneratedRegex(@"\b(presents?|presentent|anne\s+rice'?s?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SeriesFillerWordsRegex();

    [GeneratedRegex(@"(?<=\b[A-Za-z]{3,}[bcdfghjklmnpqrstvwxz])ou\b", RegexOptions.IgnoreCase)]
    private static partial Regex JapaneseLongORegex();

    [GeneratedRegex(@"^(the|les|des|une|and|this|that|with|from|world|serie|series|season|show|legend|legende)$", RegexOptions.IgnoreCase)]
    private static partial Regex SeriesStopwordRegex();

    private static bool StartsWithDistinctivePrefix(string? title, string foldedPrefix)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var folded = FoldLookupPunctuation(title);
        if (!folded.StartsWith(foldedPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return folded.Length == foldedPrefix.Length
            || !char.IsLetterOrDigit(folded[foldedPrefix.Length]);
    }

    private static bool ContainsDistinctiveToken(string? title, string foldedToken)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var folded = FoldLookupPunctuation(title);
        foreach (var token in folded.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(token, foldedToken, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsEditDistanceAtMostOne(string queryFolded, string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var folded = FoldLookupPunctuation(title);
        if (Math.Abs(folded.Length - queryFolded.Length) > 1)
            return false;

        return EditDistance(queryFolded, folded) <= 1;
    }

    private static int EditDistance(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            return 0;

        var a = left.ToLowerInvariant();
        var b = right.ToLowerInvariant();
        if (a.Length > b.Length)
            (a, b) = (b, a);

        var prev = new int[a.Length + 1];
        var curr = new int[a.Length + 1];
        for (var i = 0; i <= a.Length; i++)
            prev[i] = i;

        for (var j = 1; j <= b.Length; j++)
        {
            curr[0] = j;
            var minInRow = curr[0];
            for (var i = 1; i <= a.Length; i++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[i] = Math.Min(Math.Min(curr[i - 1] + 1, prev[i] + 1), prev[i - 1] + cost);
                if (curr[i] < minInRow)
                    minInRow = curr[i];
            }

            if (minInRow > 1)
                return 2;

            (prev, curr) = (curr, prev);
        }

        return prev[a.Length];
    }

}
