using MediaServer.Domain.Entities.Metadatas.Medias;

namespace MediaServer.Domain.Interfaces;
public interface IMovieMetadataProvider : IMetadataProviderService
{
    Task<MovieMetadata?> FetchMovieMetadata(Guid movieId, string metadataProviderExternalId, string language, CancellationToken cancellationToken);
}
