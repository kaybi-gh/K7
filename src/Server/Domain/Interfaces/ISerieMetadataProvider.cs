using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.ValueObjects;

namespace K7.Server.Domain.Interfaces;

public interface ISerieMetadataProvider
{
    string ProviderName { get; }
    Task<string?> SearchSerieAsync(MediaIdentification identification, CancellationToken cancellationToken = default);
    Task<ExternalSerieMetadata> FetchSerieMetadataAsync(string providerId, string language, CancellationToken cancellationToken = default);
    Task<ExternalSeasonMetadata> FetchSeasonMetadataAsync(string providerId, int seasonNumber, string language, CancellationToken cancellationToken = default);
    Task<ExternalEpisodeMetadata> FetchEpisodeMetadataAsync(string providerId, int seasonNumber, int episodeNumber, string language, CancellationToken cancellationToken = default);
    Task<(int Season, int Episode)?> ResolveAbsoluteEpisodeAsync(string providerId, int absoluteNumber, CancellationToken cancellationToken = default);
}
