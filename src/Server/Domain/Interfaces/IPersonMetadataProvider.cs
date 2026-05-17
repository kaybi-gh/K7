using K7.Server.Domain.Entities.Metadatas.External;

namespace K7.Server.Domain.Interfaces;

public interface IPersonMetadataProvider
{
    string ProviderName { get; }
    Task<ExternalPersonDetails?> FetchPersonAsync(string providerId, string language, CancellationToken cancellationToken = default);
}
