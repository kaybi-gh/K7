using K7.Server.Application.Common;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.Features.Medias.Services;

/// <summary>
/// Ordered serie metadata providers for search: library primary first, then the other of
/// TVDB/TMDB so identification can fall back without mixing Metadata-lane queues.
/// Auto searches both without preference.
/// </summary>
public static class SerieMetadataProviderCascade
{
    /// <summary>
    /// Providers to try for serie search / re-identify. Federation and unknown primaries stay alone.
    /// </summary>
    public static IReadOnlyList<string> ResolveSearchProviders(string? primaryProviderName)
    {
        if (string.Equals(primaryProviderName?.Trim(), "federation", StringComparison.OrdinalIgnoreCase))
            return ["federation"];

        var primary = MetadataProviderHostMapper.NormalizeProviderName(primaryProviderName);

        return primary switch
        {
            MetadataProviderNames.Auto => [MetadataProviderNames.Tmdb, MetadataProviderNames.Tvdb],
            MetadataProviderNames.Tvdb => [MetadataProviderNames.Tvdb, MetadataProviderNames.Tmdb],
            MetadataProviderNames.Tmdb => [MetadataProviderNames.Tmdb, MetadataProviderNames.Tvdb],
            MetadataProviderNames.Local => [MetadataProviderNames.Tmdb, MetadataProviderNames.Tvdb],
            _ => [primary]
        };
    }

    /// <summary>
    /// Maps library / request provider names (including Auto) to a keyed DI registration
    /// for <c>ISerieMetadataProvider</c>: tmdb, tvdb, imdb-as-tmdb, or federation.
    /// </summary>
    public static string ResolveKeyedProviderName(
        string? requestedProviderName,
        string? numberingProviderName = null,
        IEnumerable<ExternalId>? externalIds = null,
        string? requestedExternalId = null)
    {
        if (TryMapToSerieKeyedProvider(requestedProviderName, out var requested))
            return requested;

        if (!string.IsNullOrWhiteSpace(requestedExternalId) && externalIds is not null)
        {
            var matching = externalIds.FirstOrDefault(e =>
                string.Equals(e.Value, requestedExternalId, StringComparison.OrdinalIgnoreCase));
            if (matching is not null && TryMapToSerieKeyedProvider(matching.ProviderName, out var fromId))
                return fromId;
        }

        if (TryMapToSerieKeyedProvider(numberingProviderName, out var numbering))
            return numbering;

        if (externalIds is not null)
        {
            foreach (var provider in ResolveSearchProviders(requestedProviderName))
            {
                var existing = externalIds.FirstOrDefault(e =>
                    string.Equals(
                        MetadataProviderNames.Normalize(
                            MetadataProviderHostMapper.NormalizeProviderName(e.ProviderName)),
                        provider,
                        StringComparison.OrdinalIgnoreCase));
                if (existing is not null && TryMapToSerieKeyedProvider(existing.ProviderName, out var fromExisting))
                    return fromExisting;
            }
        }

        return MetadataProviderNames.Tmdb;
    }

    private static bool TryMapToSerieKeyedProvider(string? providerName, out string keyedName)
    {
        keyedName = "";
        if (string.IsNullOrWhiteSpace(providerName))
            return false;

        if (string.Equals(providerName.Trim(), "federation", StringComparison.OrdinalIgnoreCase))
        {
            keyedName = "federation";
            return true;
        }

        var normalized = MetadataProviderHostMapper.NormalizeProviderName(providerName);
        if (normalized is MetadataProviderNames.Tmdb or MetadataProviderNames.Tvdb)
        {
            keyedName = normalized;
            return true;
        }

        return false;
    }

    public static bool IsAuto(string? primaryProviderName) =>
        string.Equals(
            MetadataProviderHostMapper.NormalizeProviderName(primaryProviderName),
            MetadataProviderNames.Auto,
            StringComparison.OrdinalIgnoreCase);

    public static bool IsCascadeProvider(string? providerName, string? primaryProviderName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return false;

        var normalized = MetadataProviderNames.Normalize(
            MetadataProviderHostMapper.NormalizeProviderName(providerName));
        return ResolveSearchProviders(primaryProviderName)
            .Any(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Alternate provider used for enrichment when the canon is known.
    /// </summary>
    public static string? ResolveEnrichmentProvider(string? numberingProviderName)
    {
        var canon = MetadataProviderNames.Normalize(
            MetadataProviderHostMapper.NormalizeProviderName(numberingProviderName));
        return canon switch
        {
            MetadataProviderNames.Tvdb => MetadataProviderNames.Tmdb,
            MetadataProviderNames.Tmdb => MetadataProviderNames.Tvdb,
            _ => null
        };
    }
}
