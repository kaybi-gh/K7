using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Models;

namespace K7.Server.Domain.Interfaces;

public interface ISerieMetadataProvider
{
    string ProviderName { get; }
    Task<string?> SearchSerieAsync(
        MediaIdentification identification,
        string? language = null,
        string? fallbackLanguage = null,
        CancellationToken cancellationToken = default);
    Task<ExternalSerieMetadata> FetchSerieMetadataAsync(string providerId, string language, CancellationToken cancellationToken = default, string? fallbackLanguage = null);
    Task<ExternalSeasonMetadata> FetchSeasonMetadataAsync(string providerId, int seasonNumber, string language, CancellationToken cancellationToken = default, string? fallbackLanguage = null);
    Task<ExternalEpisodeMetadata> FetchEpisodeMetadataAsync(string providerId, int seasonNumber, int episodeNumber, string language, CancellationToken cancellationToken = default, string? fallbackLanguage = null);
    Task<(int Season, int Episode)?> ResolveAbsoluteEpisodeAsync(string providerId, int absoluteNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lightweight episode key set for Auto numbering hit-rate and bulk refresh mapping.
    /// </summary>
    Task<IReadOnlySet<(int Season, int Episode)>> ListEpisodeKeysAsync(
        string providerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds episode metadata from a previously loaded catalog when possible (avoids per-episode extended HTTP).
    /// Returns null when the provider cannot satisfy the request without a dedicated fetch.
    /// </summary>
    Task<ExternalEpisodeMetadata?> TryBuildEpisodeMetadataFromCatalogAsync(
        string providerId,
        int seasonNumber,
        int episodeNumber,
        string language,
        string? fallbackLanguage = null,
        CancellationToken cancellationToken = default);
}
