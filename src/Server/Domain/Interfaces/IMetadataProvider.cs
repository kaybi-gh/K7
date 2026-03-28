using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.ValueObjects;

namespace K7.Server.Domain.Interfaces;
public interface IMetadataProvider<T> where T : IExternalMetadata
{
    string ProviderName { get; }
    Task<string?> SearchAsync(MediaIdentification movieIdentification, CancellationToken cancellationToken = default);
    Task<T> FetchMetadata(string providerId, string language, CancellationToken cancellationToken = default);
}
