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

    public async Task<Dictionary<string, Guid>> MatchItemsAsync(
        IReadOnlyList<SourceMediaItem> items,
        CancellationToken cancellationToken = default)
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
        var providerPriority = new[] { "tmdb", "imdb", "tvdb", "musicbrainz" };

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

    public async Task<Dictionary<string, Guid>> MatchPlaylistItemsAsync(
        IReadOnlyList<SourcePlaylistItem> items,
        CancellationToken cancellationToken = default)
    {
        var asMediaItems = items.Select(i => new SourceMediaItem
        {
            Id = i.Id,
            Title = i.Title,
            ProviderIds = i.ProviderIds,
            PlayCount = 0,
            IsCompleted = false
        }).ToList();

        return await MatchItemsAsync(asMediaItems, cancellationToken);
    }
}
