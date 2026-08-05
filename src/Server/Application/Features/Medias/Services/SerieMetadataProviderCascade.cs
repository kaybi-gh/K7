using K7.Server.Application.Common;
using K7.Server.Application.Helpers;

namespace K7.Server.Application.Features.Medias.Services;

/// <summary>
/// Ordered serie metadata providers for search: library primary first, then the other of
/// TVDB/TMDB so identification can fall back without mixing Metadata-lane queues.
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
            MetadataProviderNames.Tvdb => [MetadataProviderNames.Tvdb, MetadataProviderNames.Tmdb],
            MetadataProviderNames.Tmdb => [MetadataProviderNames.Tmdb, MetadataProviderNames.Tvdb],
            MetadataProviderNames.Local => [MetadataProviderNames.Tvdb, MetadataProviderNames.Tmdb],
            _ => [primary]
        };
    }

    public static bool IsCascadeProvider(string? providerName, string? primaryProviderName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return false;

        var normalized = MetadataProviderNames.Normalize(
            MetadataProviderHostMapper.NormalizeProviderName(providerName));
        return ResolveSearchProviders(primaryProviderName)
            .Any(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase));
    }
}
