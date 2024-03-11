using MediaServer.Domain.ValueObjects;

namespace MediaServer.Domain.Interfaces;
public interface IMetadataProviderService
{
    Task<string?> SearchMetadataProviderExternalIdAsync(MediaIdentification movieIdentification, CancellationToken cancellationToken);
    Task<ICollection<MediaPicture>?> FetchMediaPictures(int metadataId, string metadataProviderExternalId, string language, CancellationToken cancellationToken, string? fallbackLanguage = "en");
}
