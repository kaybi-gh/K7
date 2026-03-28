using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos;

public sealed record MetadataProviderInfoDto
{
    public required string ProviderName { get; init; }
    public required IReadOnlyList<LibraryMediaType> SupportedMediaTypes { get; init; }
}
