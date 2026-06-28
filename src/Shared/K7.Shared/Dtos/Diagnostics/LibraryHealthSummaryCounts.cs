using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Diagnostics;

public static class LibraryHealthSummaryCounts
{
    public static int SumErrors(IEnumerable<LibraryHealthSummaryDto> summaries) =>
        summaries.Sum(l => l.OrphanIndexedFileCount + l.MediaWithoutFilesCount + l.MissingFileMetadataCount);

    public static int SumWarnings(IEnumerable<LibraryHealthSummaryDto> summaries) =>
        summaries.Sum(l => l.UnidentifiedIndexedFileCount + l.MissingHlsSegmentsCount
            + l.MediaMissingPicturesCount + l.MediaMissingExternalIdCount + l.MediaMissingMetadataCount
            + l.StaleMetadataCount + l.InaccessiblePathCount);

    public static int SumInfo(IEnumerable<LibraryHealthSummaryDto> summaries) =>
        summaries.Sum(l => l.MissingAudioAnalysisCount);

    public static int SumTotal(IEnumerable<LibraryHealthSummaryDto> summaries) =>
        SumErrors(summaries) + SumWarnings(summaries) + SumInfo(summaries);

    public static int SumLibraryIssues(LibraryHealthSummaryDto summary) =>
        summary.OrphanIndexedFileCount + summary.UnidentifiedIndexedFileCount + summary.MissingFileMetadataCount
        + summary.MissingHlsSegmentsCount + summary.MediaMissingPicturesCount + summary.MediaMissingExternalIdCount
        + summary.MediaMissingMetadataCount + summary.MediaWithoutFilesCount + summary.StaleMetadataCount
        + summary.MissingAudioAnalysisCount + summary.InaccessiblePathCount;

    public static int SumIssue(IEnumerable<LibraryHealthSummaryDto> summaries, DiagnosticIssue issue) =>
        summaries.Sum(s => CountIssue(s, issue));

    public static int SumEntityType(IEnumerable<LibraryHealthSummaryDto> summaries, DiagnosticEntityType entityType) =>
        summaries.Sum(s => CountEntityType(s, entityType));

    private static int CountIssue(LibraryHealthSummaryDto summary, DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.OrphanFile => summary.OrphanIndexedFileCount,
        DiagnosticIssue.UnidentifiedFile => summary.UnidentifiedIndexedFileCount,
        DiagnosticIssue.MissingFileMetadata => summary.MissingFileMetadataCount,
        DiagnosticIssue.MissingHlsSegments => summary.MissingHlsSegmentsCount,
        DiagnosticIssue.MissingPictures => summary.MediaMissingPicturesCount,
        DiagnosticIssue.MissingMetadata => summary.MediaMissingMetadataCount,
        DiagnosticIssue.MissingExternalId => summary.MediaMissingExternalIdCount,
        DiagnosticIssue.StaleMetadata => summary.StaleMetadataCount,
        DiagnosticIssue.MissingAudioAnalysis => summary.MissingAudioAnalysisCount,
        DiagnosticIssue.MissingFiles => summary.MediaWithoutFilesCount,
        DiagnosticIssue.InaccessiblePath => summary.InaccessiblePathCount,
        _ => 0
    };

    private static int CountEntityType(LibraryHealthSummaryDto summary, DiagnosticEntityType entityType) => entityType switch
    {
        DiagnosticEntityType.IndexedFile => summary.OrphanIndexedFileCount + summary.UnidentifiedIndexedFileCount
            + summary.MissingFileMetadataCount + summary.MissingHlsSegmentsCount,
        DiagnosticEntityType.Media => summary.MediaMissingPicturesCount + summary.MediaMissingExternalIdCount
            + summary.MediaMissingMetadataCount + summary.MediaWithoutFilesCount + summary.StaleMetadataCount
            + summary.MissingAudioAnalysisCount,
        DiagnosticEntityType.Library => summary.InaccessiblePathCount,
        _ => 0
    };
}
