using K7.Server.Domain.Entities.Metadatas.External;

namespace K7.Server.Domain.Interfaces;

public interface IMusicArtistMetadataProvider
{
    Task<ExternalMusicArtistDetails?> FetchArtistDetailsAsync(string musicBrainzArtistId, string language, CancellationToken cancellationToken = default);
}
