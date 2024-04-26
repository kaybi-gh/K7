using MediaServer.Domain.ValueObjects;

namespace MediaServer.Domain.Interfaces;
public interface IMetadataProviderService
{
    Task<string?> SearchMetadataProviderExternalIdAsync(MediaIdentification movieIdentification, CancellationToken cancellationToken);
}
