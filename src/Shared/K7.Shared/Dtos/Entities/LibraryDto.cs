using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record LibraryDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required LibraryMediaType MediaType { get; init; }
    public required string RootPath { get; init; }
    public required string MetadataProviderName { get; init; }
    public required string MetadataLanguage { get; init; }
    public required string MetadataFallbackLanguage { get; init; }
    public int? MetadataRefreshIntervalDays { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public Guid? CoverPictureId { get; init; }
    public string? CoverDominantColor { get; init; }
}
