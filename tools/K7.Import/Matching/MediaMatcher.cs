using K7.Import.Clients;
using K7.Import.Models;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Responses;

namespace K7.Import.Matching;

public sealed class MediaMatcher
{
    private readonly K7ApiClient _k7Client;

    public MediaMatcher(K7ApiClient k7Client)
    {
        _k7Client = k7Client;
    }

    public async Task<(Dictionary<string, Guid> Matches, int CreatedCount)> MatchItemsAsync(
        IReadOnlyList<SourceMediaItem> items,
        bool createMissing = false,
        bool fetchMetadata = false,
        CancellationToken cancellationToken = default)
    {
        var matched = await MatchByExternalIdsAsync(items, cancellationToken);
        var createdCount = await ResolveUnresolvedAsync(items, matched, createMissing, fetchMetadata, cancellationToken);
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
            MediaType = defaultMediaType,
            PlayCount = 0,
            IsCompleted = false
        }).ToList();

        return await MatchItemsAsync(asMediaItems, createMissing, fetchMetadata, cancellationToken);
    }

    private async Task<Dictionary<string, Guid>> MatchByExternalIdsAsync(
        IReadOnlyList<SourceMediaItem> items,
        CancellationToken cancellationToken)
    {
        var allExternalIds = items
            .SelectMany(item => item.ProviderIds.Select(kvp => new LookupMediasByExternalIdsRequest.ExternalIdItem
            {
                Provider = kvp.Key,
                Value = kvp.Value
            }))
            .DistinctBy(x => (x.Provider, x.Value))
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
            .ToDictionary(r => (r.Provider.ToLowerInvariant(), r.Value), r => r.MediaId!.Value);

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
                if (matchLookup.TryGetValue((kvp.Key.ToLowerInvariant(), kvp.Value), out var mediaId))
                {
                    matched[item.Id] = mediaId;
                    break;
                }
            }
        }

        return matched;
    }

    private async Task<int> ResolveUnresolvedAsync(
        IReadOnlyList<SourceMediaItem> items,
        Dictionary<string, Guid> matched,
        bool createMissing,
        bool fetchMetadata,
        CancellationToken cancellationToken)
    {
        var unresolved = items.Where(i => !matched.ContainsKey(i.Id)).ToList();
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
        return new BulkCreateMediasRequest.BulkCreateMediaItem
        {
            Key = item.Id,
            MediaType = item.MediaType ?? "music",
            Title = item.Title,
            Year = item.Year,
            ExternalIds = item.ProviderIds,
            ArtistName = item.ArtistName,
            AlbumName = item.AlbumName,
            SeriesTitle = item.SeriesTitle,
            SeasonNumber = item.SeasonNumber,
            EpisodeNumber = item.EpisodeNumber
        };
    }
}
