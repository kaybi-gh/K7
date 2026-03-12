using K7.Server.Domain.Entities.Metadatas.External;

namespace K7.Server.Domain.Interfaces;

public interface IMusicArtistMetadataProvider
{
    string ProviderName { get; }
    Task<ExternalMusicArtistDetails?> FetchByProviderIdAsync(string providerId, string language, CancellationToken cancellationToken = default);
    Task<ExternalMusicArtistDetails?> SearchByNameAsync(string artistName, string language, CancellationToken cancellationToken = default);
}
