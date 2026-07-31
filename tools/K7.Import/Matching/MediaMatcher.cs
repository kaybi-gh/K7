using K7.Import.Clients;
using K7.Import.Models;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Responses;
using Spectre.Console;

namespace K7.Import.Matching;

public sealed class MediaMatcher
{
    private readonly K7ApiClient _k7Client;
    private readonly IReadOnlyList<(string PlexPrefix, string K7Prefix)> _pathMaps;

    public MediaMatcher(K7ApiClient k7Client, IReadOnlyList<(string PlexPrefix, string K7Prefix)>? pathMaps = null)
    {
        _k7Client = k7Client;
        _pathMaps = pathMaps ?? [];
    }

    public int MatchedByExternalId { get; private set; }
    public int MatchedByPath { get; private set; }
    public int MatchedByTitleOrExisting { get; private set; }

    public async Task<(Dictionary<string, Guid> Matches, int CreatedCount)> MatchItemsAsync(
        IReadOnlyList<SourceMediaItem> items,
        bool createMissing = false,
        bool fetchMetadata = false,
        CancellationToken cancellationToken = default)
    {
        var matched = await MatchByExternalIdsAsync(items, cancellationToken);
        MatchedByExternalId += matched.Count;

        var pathMatched = await MatchByPathsAsync(items, matched, cancellationToken);
        MatchedByPath += pathMatched;

        var beforeResolve = matched.Count;
        var createdCount = await ResolveUnresolvedAsync(items, matched, createMissing, fetchMetadata, cancellationToken);
        MatchedByTitleOrExisting += matched.Count - beforeResolve - createdCount;

        return (matched, createdCount);
    }

    public async Task<(Dictionary<string, Guid> Matches, int CreatedCount)> MatchPlaylistItemsAsync(
        IReadOnlyList<SourcePlaylistItem> items,
        string defaultMediaType = "music",
        bool createMissing = false,
        bool fetchMetadata = false,
        CancellationToken cancellationToken = default)
    {
        var asMediaItems = items.Select(i => new SourceMediaItem
        {
            Id = i.Id,
            Title = i.Title,
            ProviderIds = i.ProviderIds,
            FilePaths = i.FilePaths,
            ArtistName = i.ArtistName,
            AlbumName = i.AlbumName,
            Year = i.Year,
            SeriesTitle = i.SeriesTitle,
            SeasonNumber = i.SeasonNumber,
            EpisodeNumber = i.EpisodeNumber,
            MediaType = defaultMediaType,
            PlayCount = 0,
            IsCompleted = false
        }).ToList();

        return await MatchItemsAsync(asMediaItems, createMissing, fetchMetadata, cancellationToken);
    }

    public static List<(string PlexPrefix, string K7Prefix)> ParsePathMaps(IEnumerable<string> maps)
    {
        var result = new List<(string, string)>();
        foreach (var map in maps)
        {
            var idx = map.IndexOf(':');
            // Allow Windows drive letters: D:/plex:E:/k7 — split on last colon that separates two paths
            // Format is plexPrefix:k7Prefix where each may contain colons (drive letters).
            // Prefer splitting on ":" only when both sides look like paths: use first ':' after a path separator or at position > 1.
            if (!TrySplitPathMap(map, out var plex, out var k7))
                throw new ArgumentException($"Invalid --path-map '{map}'. Expected plexPrefix:k7Prefix.");

            result.Add((NormalizePath(plex).TrimEnd('/'), NormalizePath(k7).TrimEnd('/')));
        }

        return result;
    }

    private static bool TrySplitPathMap(string map, out string plex, out string k7)
    {
        plex = "";
        k7 = "";
        // Prefer "plex=>k7" if present for unambiguous Windows paths
        var arrow = map.IndexOf("=>", StringComparison.Ordinal);
        if (arrow > 0)
        {
            plex = map[..arrow];
            k7 = map[(arrow + 2)..];
            return plex.Length > 0 && k7.Length > 0;
        }

        // Split on the colon that sits between two absolute-looking segments.
        for (var i = 1; i < map.Length - 1; i++)
        {
            if (map[i] != ':')
                continue;

            var left = map[..i];
            var right = map[(i + 1)..];
            if (LooksLikePath(left) && LooksLikePath(right))
            {
                plex = left;
                k7 = right;
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikePath(string value) =>
        value.Contains('/') || value.Contains('\\') || (value.Length >= 2 && value[1] == ':');

    public async Task<IReadOnlyList<(string PlexPrefix, string K7Prefix)>> AutoDeducePathMapsAsync(
        IReadOnlyList<SourceMediaItem> items,
        CancellationToken cancellationToken = default)
    {
        var plexPaths = items
            .SelectMany(i => i.FilePaths)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(200)
            .ToList();

        if (plexPaths.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Path matching: no Plex file paths on items (cannot auto-deduce --path-map).[/]");
            return [];
        }

        var fileNames = plexPaths
            .Select(p => p.Split('/').Last())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToList();

        var direct = await _k7Client.LookupMediasByPathsAsync(plexPaths, cancellationToken);
        if (direct.Count(r => r.MediaId.HasValue) >= Math.Max(3, plexPaths.Count / 10))
            return [];

        var byName = await _k7Client.LookupIndexedPathsByFileNamesAsync(fileNames, cancellationToken);
        var withHits = byName.Count(kv => kv.Value.Count > 0);
        if (withHits == 0)
        {
            AnsiConsole.MarkupLine(
                "[yellow]Path matching: no K7 files matched by name. Pass explicit --path-map " +
                "(e.g. /data/media/Videos=>/media).[/]");
            return [];
        }

        var votes = new Dictionary<(string, string), int>();
        foreach (var plexPath in plexPaths)
        {
            var name = plexPath.Split('/').Last();
            if (!byName.TryGetValue(name, out var k7Candidates))
                continue;

            foreach (var k7Path in k7Candidates)
            {
                if (!TryInferPrefixMap(plexPath, NormalizePath(k7Path), out var plexPrefix, out var k7Prefix))
                    continue;

                var key = (plexPrefix, k7Prefix);
                votes[key] = votes.GetValueOrDefault(key) + 1;
            }
        }

        var best = votes
            .OrderByDescending(v => v.Value)
            .Where(v => v.Value >= 2)
            .Take(5)
            .Select(v => v.Key)
            .ToList();

        foreach (var (plexPrefix, k7Prefix) in best)
            AnsiConsole.MarkupLine($"[dim]Auto path-map: {Markup.Escape(plexPrefix)} => {Markup.Escape(k7Prefix)}[/]");

        return best;
    }

    private static bool TryInferPrefixMap(string plexPath, string k7Path, out string plexPrefix, out string k7Prefix)
    {
        plexPrefix = "";
        k7Prefix = "";
        var plexParts = plexPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var k7Parts = k7Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var i = 0;
        while (i < plexParts.Length && i < k7Parts.Length
               && string.Equals(plexParts[plexParts.Length - 1 - i], k7Parts[k7Parts.Length - 1 - i], StringComparison.OrdinalIgnoreCase))
        {
            i++;
        }

        if (i < 2)
            return false;

        var plexHead = string.Join('/', plexParts.Take(plexParts.Length - i));
        var k7Head = string.Join('/', k7Parts.Take(k7Parts.Length - i));
        // Preserve leading slash if originals had absolute unix paths
        if (plexPath.StartsWith('/')) plexHead = "/" + plexHead;
        if (k7Path.StartsWith('/')) k7Head = "/" + k7Head;

        plexPrefix = plexHead.TrimEnd('/');
        k7Prefix = k7Head.TrimEnd('/');
        return plexPrefix.Length > 0 && k7Prefix.Length > 0 && !string.Equals(plexPrefix, k7Prefix, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, Guid>> MatchByExternalIdsAsync(
        IReadOnlyList<SourceMediaItem> items,
        CancellationToken cancellationToken)
    {
        var allExternalIds = items
            .SelectMany(item => item.ProviderIds
                .Where(kvp => kvp.Key is not "musicbrainz-release" and not "musicbrainz-track")
                .Select(kvp => new LookupMediasByExternalIdsRequest.ExternalIdItem
                {
                    Provider = kvp.Key,
                    Value = kvp.Value
                }))
            .DistinctBy(x => (x.Provider.ToLowerInvariant(), x.Value))
            .ToList();

        if (allExternalIds.Count == 0)
            return new Dictionary<string, Guid>();

        var results = new List<ExternalIdMatchResult>();
        const int chunkSize = 500;

        for (var i = 0; i < allExternalIds.Count; i += chunkSize)
        {
            var chunk = allExternalIds.Skip(i).Take(chunkSize).ToList();
            var chunkResults = await _k7Client.LookupMediasByExternalIdsAsync(chunk, cancellationToken);
            results.AddRange(chunkResults);
        }

        var matchLookup = results
            .Where(r => r.MediaId.HasValue)
            .GroupBy(r => (r.Provider.ToLowerInvariant(), r.Value))
            .ToDictionary(g => g.Key, g => g.First().MediaId!.Value);

        var matched = new Dictionary<string, Guid>();
        var providerPriority = new[] { "tmdb", "imdb", "tvdb", "musicbrainz", "isrc", "spotify" };

        foreach (var item in items)
        {
            foreach (var provider in providerPriority)
            {
                if (item.ProviderIds.TryGetValue(provider, out var value) &&
                    matchLookup.TryGetValue((provider, value), out var mediaId))
                {
                    matched[item.Id] = mediaId;
                    break;
                }
            }

            if (matched.ContainsKey(item.Id))
                continue;

            foreach (var kvp in item.ProviderIds)
            {
                if (kvp.Key is "musicbrainz-release" or "musicbrainz-track")
                    continue;

                if (matchLookup.TryGetValue((kvp.Key.ToLowerInvariant(), kvp.Value), out var mediaId))
                {
                    matched[item.Id] = mediaId;
                    break;
                }
            }
        }

        return matched;
    }

    private async Task<int> MatchByPathsAsync(
        IReadOnlyList<SourceMediaItem> items,
        Dictionary<string, Guid> matched,
        CancellationToken cancellationToken)
    {
        var unresolved = items.Where(i => !matched.ContainsKey(i.Id) && i.FilePaths.Count > 0).ToList();
        if (unresolved.Count == 0)
            return 0;

        var remapped = new List<(string ItemId, string Path)>();
        foreach (var item in unresolved)
        {
            foreach (var plexPath in item.FilePaths)
            {
                foreach (var mapped in RemapPath(plexPath))
                    remapped.Add((item.Id, mapped));
            }
        }

        if (remapped.Count == 0)
            return 0;

        var uniquePaths = remapped.Select(r => r.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var pathResults = await _k7Client.LookupMediasByPathsAsync(uniquePaths, cancellationToken);
        var pathToMedia = pathResults
            .Where(r => r.MediaId.HasValue)
            .GroupBy(r => r.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().MediaId!.Value, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var group in remapped.GroupBy(r => r.ItemId))
        {
            if (matched.ContainsKey(group.Key))
                continue;

            foreach (var (_, path) in group)
            {
                if (pathToMedia.TryGetValue(path, out var mediaId))
                {
                    matched[group.Key] = mediaId;
                    added++;
                    break;
                }
            }
        }

        return added;
    }

    private IEnumerable<string> RemapPath(string plexPath)
    {
        var normalized = NormalizePath(plexPath);
        yield return normalized;

        foreach (var (plexPrefix, k7Prefix) in _pathMaps)
        {
            if (normalized.StartsWith(plexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var suffix = normalized[plexPrefix.Length..].TrimStart('/');
                yield return string.IsNullOrEmpty(suffix) ? k7Prefix : $"{k7Prefix}/{suffix}";
            }
        }
    }

    private async Task<int> ResolveUnresolvedAsync(
        IReadOnlyList<SourceMediaItem> items,
        Dictionary<string, Guid> matched,
        bool createMissing,
        bool fetchMetadata,
        CancellationToken cancellationToken)
    {
        var unresolved = items
            .Where(i => !matched.ContainsKey(i.Id))
            .Where(i => i.MediaType is "movie" or "music" or "episode" or "serie")
            .ToList();
        if (unresolved.Count == 0)
            return 0;

        var bulkItems = unresolved.Select(ToBulkCreateItem).ToList();
        var result = await _k7Client.BulkCreateMediasAsync(
            bulkItems,
            fetchMetadata,
            createMissing,
            cancellationToken);

        var createdCount = 0;
        foreach (var r in result.Results)
        {
            if (r.MediaId == Guid.Empty)
                continue;

            matched.TryAdd(r.Key, r.MediaId);
            if (r.WasCreated)
                createdCount++;
        }

        return createdCount;
    }

    private static BulkCreateMediasRequest.BulkCreateMediaItem ToBulkCreateItem(SourceMediaItem item)
    {
        // Do not persist musicbrainz-release onto virtual media as musicbrainz (RG namespace).
        var externalIds = item.ProviderIds
            .Where(kvp => kvp.Key is not "musicbrainz-track")
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        return new BulkCreateMediasRequest.BulkCreateMediaItem
        {
            Key = item.Id,
            MediaType = item.MediaType ?? "music",
            Title = item.Title,
            Year = item.Year,
            ExternalIds = externalIds,
            ArtistName = item.ArtistName,
            AlbumName = item.AlbumName,
            SeriesTitle = item.SeriesTitle,
            SeasonNumber = item.SeasonNumber,
            EpisodeNumber = item.EpisodeNumber
        };
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').Trim();
}
