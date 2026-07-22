using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Diagnostics;

public sealed record LibraryHealthSummaryDto
{
    public required Guid LibraryId { get; init; }
    public required string LibraryTitle { get; init; }
    public required LibraryMediaType MediaType { get; init; }

    public required int TotalMediaCount { get; init; }
    public required int MediaMissingPicturesCount { get; init; }
    public required int MediaMissingExternalIdCount { get; init; }
    public required int MediaMissingMetadataCount { get; init; }
    public required int MediaWithoutFilesCount { get; init; }
    public required int StaleMetadataCount { get; init; }

    public required int TotalIndexedFileCount { get; init; }
    public required int OrphanIndexedFileCount { get; init; }
    public required int UnidentifiedIndexedFileCount { get; init; }
    public required int MissingFileMetadataCount { get; init; }
    public required int MissingHlsSegmentsCount { get; init; }
    public required int MissingChaptersCount { get; init; }
    public required int MissingThemeSongCount { get; init; }
    public required int MissingIntroOutroCount { get; init; }

    public required int MissingAudioAnalysisCount { get; init; }

    public required int InaccessiblePathCount { get; init; }

    public required int PendingBackgroundTaskCount { get; init; }
    public required int FailedBackgroundTaskCount { get; init; }
}
