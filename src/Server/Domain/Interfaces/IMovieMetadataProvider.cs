using K7.Server.Domain.Entities.Metadatas.Medias;

namespace K7.Server.Domain.Interfaces;
public interface IMovieMetadataProvider : IMetadataProviderService
{
    Task<MovieMetadata?> FetchMovieMetadata(Guid movieId, string metadataProviderExternalId, string language, CancellationToken cancellationToken);
}
