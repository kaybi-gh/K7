using MediaServer.Domain.ValueObjects;

namespace MediaServer.Domain.Interfaces;
public interface IMetadataProviderService
{
    Task<string?> SearchMetadataProviderExternalIdAsync(MediaIdentification movieIdentification, CancellationToken cancellationToken);
    Task<ICollection<MetadataPicture>?> FetchMetadataPictures(int metadataId, string metadataProviderExternalId, string language, CancellationToken cancellationToken, string? fallbackLanguage = "en");
}
