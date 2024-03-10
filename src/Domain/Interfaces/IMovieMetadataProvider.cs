using MediaServer.Domain.Entities.Metadatas;

namespace MediaServer.Domain.Interfaces;
public interface IMovieMetadataProvider : IMetadataProviderService
{
    Task<MovieMetadata?> FetchMovieMetadata(int movieId, string metadataProviderExternalId, string language, CancellationToken cancellationToken);
}
