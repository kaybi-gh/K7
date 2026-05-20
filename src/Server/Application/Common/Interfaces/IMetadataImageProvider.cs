using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Metadatas;

namespace K7.Server.Application.Common.Interfaces;

public record ImageProviderContext(
    MediaType MediaType,
    string ProviderId,
    string Language,
    int? SeasonNumber = null,
    int? EpisodeNumber = null);

public interface IMetadataImageProvider
{
    string ProviderName { get; }
    bool SupportsMediaType(MediaType mediaType);
    Task<IReadOnlyList<ProviderImageDto>> GetImagesAsync(ImageProviderContext context, CancellationToken cancellationToken = default);
}
