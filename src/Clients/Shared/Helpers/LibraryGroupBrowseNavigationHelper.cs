using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Rules;

namespace K7.Clients.Shared.Helpers;

public static class LibraryGroupBrowseNavigationHelper
{
    public static readonly string[] BrowseQueryKeys =
    [
        "mediaType",
        "sort",
        "view",
        "filter",
        "isearch",
        "genre",
        "studio",
        "network",
        "source"
    ];

    public static Guid? ResolveGroupId(
        IReadOnlyList<LibraryGroupDto> groups,
        Guid? libraryId,
        LibraryMediaType mediaType)
    {
        if (libraryId.HasValue)
        {
            var match = groups.FirstOrDefault(g =>
                g.MediaType == mediaType && g.LibraryIds.Contains(libraryId.Value));
            if (match is not null)
                return match.Id;
        }

        return groups.FirstOrDefault(g => g.MediaType == mediaType)?.Id;
    }

    public static LibraryMediaType ToLibraryMediaType(MediaType mediaType) =>
        mediaType switch
        {
            MediaType.Movie => LibraryMediaType.Movie,
            MediaType.Serie or MediaType.SerieSeason or MediaType.SerieEpisode => LibraryMediaType.Serie,
            _ => LibraryMediaType.Music
        };

    public static string BuildBrowseUrl(
        Guid groupId,
        string? genre = null,
        string? studio = null,
        string? network = null,
        MediaType? mediaType = null) =>
        BuildBrowseUrl(groupId, new LibraryGroupBrowseUrlState(
            MediaType: mediaType,
            Filter: BuildLegacySeedFilter(genre, studio, network)));

    public static string BuildBrowseUrl(Guid groupId, LibraryGroupBrowseUrlState? state)
    {
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (state?.MediaType is MediaType mediaType && mediaType != default)
            query["mediaType"] = mediaType.ToString();

        if (state?.Sort is MediaOrderingOption sort)
        {
            var sortValue = BrowseSortUrlMapping.ToUrlValue(sort);
            if (!string.IsNullOrEmpty(sortValue))
                query["sort"] = sortValue;
        }

        if (state?.View is BrowseViewMode view && view != BrowseViewMode.Grid)
            query["view"] = view.ToString().ToLowerInvariant();

        if (state?.Filter is { } filter && !MediaBrowseFilterPresets.IsEmpty(filter))
            query["filter"] = FilterUrlCodec.Encode(filter);

        if (state?.IntelligentSearch is { } intelligentSearch)
            query["isearch"] = FilterUrlCodec.Encode(intelligentSearch);

        if (!string.IsNullOrWhiteSpace(state?.ContentSource))
            query["source"] = state.ContentSource;

        if (!query.ContainsKey("filter"))
        {
            ApplyLegacySeedQuery(query, state?.Filter);
        }

        return query.Count == 0
            ? $"/library-groups/{groupId}"
            : $"/library-groups/{groupId}?{string.Join("&", query.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}"))}";
    }

    public static LibraryGroupBrowseUrlState ParseBrowseState(IReadOnlyDictionary<string, string> query)
    {
        MediaType? mediaType = null;
        if (query.TryGetValue("mediaType", out var mediaTypeValue)
            && Enum.TryParse<MediaType>(mediaTypeValue, ignoreCase: true, out var parsedMediaType))
        {
            mediaType = parsedMediaType;
        }

        BrowseViewMode? view = null;
        if (query.TryGetValue("view", out var viewValue)
            && Enum.TryParse<BrowseViewMode>(viewValue, ignoreCase: true, out var parsedView))
        {
            view = parsedView;
        }

        var sort = query.TryGetValue("sort", out var sortValue)
            ? BrowseSortUrlMapping.FromUrlValue(sortValue)
            : MediaOrderingOption.TitleAsc;

        RuleGroupDto? filter = null;
        if (query.TryGetValue("filter", out var filterValue))
        {
            filter = FilterUrlCodec.Decode<RuleGroupDto>(filterValue);
        }
        else
        {
            filter = BuildLegacySeedFilter(
                query.GetValueOrDefault("genre"),
                query.GetValueOrDefault("studio"),
                query.GetValueOrDefault("network"));
        }

        IntelligentSearchRequest? intelligentSearch = query.TryGetValue("isearch", out var isearchValue)
            ? FilterUrlCodec.Decode<IntelligentSearchRequest>(isearchValue)
            : null;

        string? contentSource = null;
        if (query.TryGetValue("source", out var sourceValue) && !string.IsNullOrWhiteSpace(sourceValue))
            contentSource = sourceValue;

        return new LibraryGroupBrowseUrlState(mediaType, sort, view, filter, intelligentSearch, contentSource);
    }

    private static RuleGroupDto BuildLegacySeedFilter(string? genre, string? studio, string? network)
    {
        var filter = MediaBrowseFilterPresets.Empty;
        if (!string.IsNullOrWhiteSpace(genre))
            filter = MediaBrowseFilterPresets.ToggleGenre(filter, genre);
        if (!string.IsNullOrWhiteSpace(studio))
            filter = MediaBrowseFilterPresets.SetSearchFieldValue(filter, "Studio", studio);
        if (!string.IsNullOrWhiteSpace(network))
            filter = MediaBrowseFilterPresets.SetSearchFieldValue(filter, "Network", network);

        return filter;
    }

    private static void ApplyLegacySeedQuery(IDictionary<string, string?> query, RuleGroupDto? filter)
    {
        if (filter is null || MediaBrowseFilterPresets.IsEmpty(filter))
            return;

        foreach (var genre in MediaBrowseFilterPresets.GetSelectedGenres(filter))
            query["genre"] = genre;

        var studio = MediaBrowseFilterPresets.GetSearchFieldValue(filter, "Studio");
        if (!string.IsNullOrWhiteSpace(studio))
            query["studio"] = studio;

        var network = MediaBrowseFilterPresets.GetSearchFieldValue(filter, "Network");
        if (!string.IsNullOrWhiteSpace(network))
            query["network"] = network;
    }
}
