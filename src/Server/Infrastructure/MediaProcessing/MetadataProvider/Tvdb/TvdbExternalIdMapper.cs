using K7.Server.Domain.Entities;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;

internal static class TvdbExternalIdMapper
{
    public static List<ExternalId> BuildExternalIds(string primaryTvdbId, IReadOnlyList<TvdbRemoteId>? remoteIds)
    {
        var ids = new List<ExternalId>
        {
            new() { ProviderName = "tvdb", Value = primaryTvdbId }
        };

        if (remoteIds is null)
            return ids;

        foreach (var remote in remoteIds)
        {
            if (string.IsNullOrWhiteSpace(remote.Id) || string.IsNullOrWhiteSpace(remote.SourceName))
                continue;

            var providerName = MapRemoteSourceToProviderName(remote.SourceName);
            if (providerName is null || ids.Any(i => i.ProviderName == providerName))
                continue;

            ids.Add(new ExternalId { ProviderName = providerName, Value = remote.Id.Trim() });
        }

        return ids;
    }

    internal static string? MapRemoteSourceToProviderName(string sourceName)
    {
        var normalized = sourceName.Trim().ToLowerInvariant();

        return normalized switch
        {
            "imdb" or "imdb id" or "imdb.com" => "imdb",
            "tmdb" or "themoviedb" or "the movie db" or "themoviedb.com" => "tmdb",
            "tvmaze" or "tv maze" or "tvmaze.com" => "tvmaze",
            "eidr" or "eidr.com" => "eidr",
            "wikidata" or "wikidata.org" => "wikidata",
            "wikipedia" or "wikipedia.org" => "wikipedia",
            _ => null
        };
    }
}
