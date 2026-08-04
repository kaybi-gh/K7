using TMDbLib.Objects.Search;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider;

/// <summary>
/// Merges TMDb search hits from multiple language queries into one candidate list.
/// </summary>
internal static class TmdbMultiLanguageSearchMerger
{
    public sealed record MergedHit(
        int Id,
        string Title,
        string? OriginalTitle,
        DateTime? ReleaseDate,
        double Popularity,
        IReadOnlyList<string> AlternateTitles,
        string? PosterPath,
        string? Overview);

    public static IReadOnlyList<MergedHit> MergeMovies(
        IReadOnlyList<IReadOnlyList<SearchMovie>> languageResults)
        => Merge(
            languageResults,
            movie => movie.Id,
            movie => movie.Title,
            movie => movie.OriginalTitle,
            movie => movie.ReleaseDate,
            movie => movie.Popularity,
            movie => movie.PosterPath,
            movie => movie.Overview);

    public static IReadOnlyList<MergedHit> MergeTv(
        IReadOnlyList<IReadOnlyList<SearchTv>> languageResults)
        => Merge(
            languageResults,
            show => show.Id,
            show => show.Name,
            show => show.OriginalName,
            show => show.FirstAirDate,
            show => show.Popularity,
            show => show.PosterPath,
            show => show.Overview);

    private static IReadOnlyList<MergedHit> Merge<T>(
        IReadOnlyList<IReadOnlyList<T>> languageResults,
        Func<T, int> idSelector,
        Func<T, string?> titleSelector,
        Func<T, string?> originalTitleSelector,
        Func<T, DateTime?> dateSelector,
        Func<T, double> popularitySelector,
        Func<T, string?> posterSelector,
        Func<T, string?> overviewSelector)
    {
        var byId = new Dictionary<int, MutableHit>();
        var order = new List<int>();

        // languageResults[0] is the preferred (library) language for display fields.
        for (var languageIndex = 0; languageIndex < languageResults.Count; languageIndex++)
        {
            var preferred = languageIndex == 0;
            foreach (var item in languageResults[languageIndex])
            {
                var id = idSelector(item);
                var title = titleSelector(item);
                var originalTitle = originalTitleSelector(item);
                var date = dateSelector(item);
                var popularity = popularitySelector(item);
                var poster = posterSelector(item);
                var overview = overviewSelector(item);

                if (!byId.TryGetValue(id, out var hit))
                {
                    hit = new MutableHit
                    {
                        Id = id,
                        Title = title ?? originalTitle ?? string.Empty,
                        OriginalTitle = originalTitle,
                        ReleaseDate = date,
                        Popularity = popularity,
                        PosterPath = poster,
                        Overview = overview
                    };
                    AddTitle(hit, title);
                    AddTitle(hit, originalTitle);
                    byId[id] = hit;
                    order.Add(id);
                    continue;
                }

                AddTitle(hit, title);
                AddTitle(hit, originalTitle);
                if (popularity > hit.Popularity)
                    hit.Popularity = popularity;
                if (hit.ReleaseDate is null && date is not null)
                    hit.ReleaseDate = date;

                // Prefer primary-language display fields when present.
                if (preferred)
                {
                    if (!string.IsNullOrWhiteSpace(title))
                        hit.Title = title;
                    if (!string.IsNullOrWhiteSpace(poster))
                        hit.PosterPath = poster;
                    if (!string.IsNullOrWhiteSpace(overview))
                        hit.Overview = overview;
                    if (!string.IsNullOrWhiteSpace(originalTitle))
                        hit.OriginalTitle = originalTitle;
                }
            }
        }

        return order
            .Select(id =>
            {
                var hit = byId[id];
                return new MergedHit(
                    hit.Id,
                    hit.Title,
                    hit.OriginalTitle,
                    hit.ReleaseDate,
                    hit.Popularity,
                    hit.Titles.ToList(),
                    hit.PosterPath,
                    hit.Overview);
            })
            .ToList();
    }

    private static void AddTitle(MutableHit hit, string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        foreach (var existing in hit.Titles)
        {
            if (string.Equals(existing, title, StringComparison.OrdinalIgnoreCase))
                return;
        }

        hit.Titles.Add(title);
    }

    private sealed class MutableHit
    {
        public required int Id { get; init; }
        public string Title { get; set; } = string.Empty;
        public string? OriginalTitle { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public double Popularity { get; set; }
        public string? PosterPath { get; set; }
        public string? Overview { get; set; }
        public List<string> Titles { get; } = [];
    }
}
