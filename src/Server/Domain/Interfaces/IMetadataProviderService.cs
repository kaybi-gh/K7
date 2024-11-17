using K7.Server.Domain.ValueObjects;

namespace K7.Server.Domain.Interfaces;
public interface IMetadataProviderService
{
    Task<string?> SearchMetadataProviderExternalIdAsync(MediaIdentification movieIdentification, CancellationToken cancellationToken);
}
