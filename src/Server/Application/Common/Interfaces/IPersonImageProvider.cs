using K7.Shared.Dtos.Entities.Metadatas;

namespace K7.Server.Application.Common.Interfaces;

public interface IPersonImageProvider
{
    string ProviderName { get; }
    Task<IReadOnlyList<ProviderImageDto>> GetPersonImagesAsync(string providerId, string language, CancellationToken cancellationToken = default);
}
