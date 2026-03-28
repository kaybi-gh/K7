using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Interfaces;

public interface IMetadataProviderInfo
{
    string ProviderName { get; }
    IReadOnlyList<LibraryMediaType> SupportedMediaTypes { get; }
}
