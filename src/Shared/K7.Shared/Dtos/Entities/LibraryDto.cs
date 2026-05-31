using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record LibraryDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required LibraryMediaType MediaType { get; init; }
    public string? RootPath { get; init; }
    public required string MetadataProviderName { get; init; }
    public required string MetadataLanguage { get; init; }
    public required string MetadataFallbackLanguage { get; init; }
    public int? MetadataRefreshIntervalDays { get; init; }
    public required Guid LibraryGroupId { get; init; }
    public Guid? PeerServerId { get; init; }
    public string? PeerServerBaseUrl { get; init; }
}
