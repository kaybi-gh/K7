using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Diagnostics;

public sealed record DiagnosticItemDto
{
    public required Guid EntityId { get; init; }
    public required string EntityName { get; init; }
    public required DiagnosticEntityType EntityType { get; init; }
    public required Guid LibraryId { get; init; }
    public required string LibraryTitle { get; init; }
    public required IReadOnlyList<DiagnosticIssue> Issues { get; init; }
    public required DiagnosticSeverity Severity { get; init; }

    public string? DetailText { get; init; }
    public string? MediaUrl { get; init; }
    public MediaType? MediaType { get; init; }
    public IReadOnlyList<string>? MissingPictureTypes { get; init; }
    public DateTimeOffset? LastMetadataRefreshedAt { get; init; }
    public int? MetadataRefreshIntervalDays { get; init; }
}
