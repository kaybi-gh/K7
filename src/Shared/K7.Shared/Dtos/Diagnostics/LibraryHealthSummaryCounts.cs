using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Diagnostics;

[Flags]
public enum DiagnosticsFilterExclusions
{
    None = 0,
    Severity = 1,
    Library = 2,
    EntityType = 4,
    Issue = 8
}

public readonly record struct DiagnosticsFilterContext(
    Guid? LibraryId = null,
    DiagnosticEntityType? EntityType = null,
    DiagnosticIssue? Issue = null,
    IReadOnlyCollection<DiagnosticIssue>? SeverityIssues = null);

public static class LibraryHealthSummaryCounts
{
    public static readonly DiagnosticIssue[] ErrorIssues =
    [
        DiagnosticIssue.OrphanFile,
        DiagnosticIssue.MissingFiles,
        DiagnosticIssue.MissingFileMetadata
    ];

    public static readonly DiagnosticIssue[] WarningIssues =
    [
        DiagnosticIssue.UnidentifiedFile,
        DiagnosticIssue.MissingHlsSegments,
        DiagnosticIssue.MissingChapters,
        DiagnosticIssue.MissingPictures,
        DiagnosticIssue.MissingMetadata,
        DiagnosticIssue.MissingExternalId,
        DiagnosticIssue.StaleMetadata,
        DiagnosticIssue.InaccessiblePath
    ];

    public static readonly DiagnosticIssue[] InfoIssues =
    [
        DiagnosticIssue.MissingAudioAnalysis
    ];

    public static int SumErrors(IEnumerable<LibraryHealthSummaryDto> summaries) =>
        summaries.Sum(l => l.OrphanIndexedFileCount + l.MediaWithoutFilesCount + l.MissingFileMetadataCount);

    public static int SumWarnings(IEnumerable<LibraryHealthSummaryDto> summaries) =>
        summaries.Sum(l => l.UnidentifiedIndexedFileCount + l.MissingHlsSegmentsCount + l.MissingChaptersCount
            + l.MediaMissingPicturesCount + l.MediaMissingExternalIdCount + l.MediaMissingMetadataCount
            + l.StaleMetadataCount + l.InaccessiblePathCount);

    public static int SumInfo(IEnumerable<LibraryHealthSummaryDto> summaries) =>
        summaries.Sum(l => l.MissingAudioAnalysisCount);

    public static int SumTotal(IEnumerable<LibraryHealthSummaryDto> summaries) =>
        SumErrors(summaries) + SumWarnings(summaries) + SumInfo(summaries);

    public static int SumLibraryIssues(LibraryHealthSummaryDto summary) =>
        summary.OrphanIndexedFileCount + summary.UnidentifiedIndexedFileCount + summary.MissingFileMetadataCount
        + summary.MissingHlsSegmentsCount + summary.MissingChaptersCount + summary.MediaMissingPicturesCount + summary.MediaMissingExternalIdCount
        + summary.MediaMissingMetadataCount + summary.MediaWithoutFilesCount + summary.StaleMetadataCount
        + summary.MissingAudioAnalysisCount + summary.InaccessiblePathCount;

    public static int SumIssue(IEnumerable<LibraryHealthSummaryDto> summaries, DiagnosticIssue issue) =>
        summaries.Sum(s => CountIssue(s, issue));

    public static int SumEntityType(IEnumerable<LibraryHealthSummaryDto> summaries, DiagnosticEntityType entityType) =>
        summaries.Sum(s => CountEntityType(s, entityType));

    public static int SumSeverity(
        IEnumerable<LibraryHealthSummaryDto> summaries,
        IReadOnlyCollection<DiagnosticIssue> severityIssues,
        DiagnosticsFilterContext context,
        DiagnosticsFilterExclusions exclusions) =>
        severityIssues.Sum(issue => SumIssue(summaries, issue, context, exclusions | DiagnosticsFilterExclusions.Severity));

    public static int SumIssue(
        IEnumerable<LibraryHealthSummaryDto> summaries,
        DiagnosticIssue issue,
        DiagnosticsFilterContext context,
        DiagnosticsFilterExclusions exclusions)
    {
        if (!MatchesFilters(issue, context, exclusions))
            return 0;

        var filtered = FilterLibraries(summaries, context, exclusions);
        return filtered.Sum(s => CountIssue(s, issue));
    }

    public static int SumEntityType(
        IEnumerable<LibraryHealthSummaryDto> summaries,
        DiagnosticEntityType entityType,
        DiagnosticsFilterContext context,
        DiagnosticsFilterExclusions exclusions) =>
        Enum.GetValues<DiagnosticIssue>()
            .Where(issue => IssueBelongsToEntityType(issue, entityType))
            .Sum(issue => SumIssue(summaries, issue, context, exclusions | DiagnosticsFilterExclusions.EntityType));

    public static int SumLibraryIssues(
        LibraryHealthSummaryDto summary,
        DiagnosticsFilterContext context,
        DiagnosticsFilterExclusions exclusions) =>
        Enum.GetValues<DiagnosticIssue>()
            .Sum(issue => SumIssue([summary], issue, context, exclusions | DiagnosticsFilterExclusions.Library));

    private static IEnumerable<LibraryHealthSummaryDto> FilterLibraries(
        IEnumerable<LibraryHealthSummaryDto> summaries,
        DiagnosticsFilterContext context,
        DiagnosticsFilterExclusions exclusions)
    {
        if (!exclusions.HasFlag(DiagnosticsFilterExclusions.Library) && context.LibraryId.HasValue)
            summaries = summaries.Where(s => s.LibraryId == context.LibraryId.Value);

        return summaries;
    }

    private static bool MatchesFilters(
        DiagnosticIssue issue,
        DiagnosticsFilterContext context,
        DiagnosticsFilterExclusions exclusions)
    {
        if (!exclusions.HasFlag(DiagnosticsFilterExclusions.Issue)
            && context.Issue.HasValue
            && context.Issue != issue)
        {
            return false;
        }

        if (!exclusions.HasFlag(DiagnosticsFilterExclusions.Severity)
            && context.SeverityIssues is { Count: > 0 }
            && !context.SeverityIssues.Contains(issue))
        {
            return false;
        }

        if (!exclusions.HasFlag(DiagnosticsFilterExclusions.EntityType)
            && context.EntityType.HasValue
            && !IssueBelongsToEntityType(issue, context.EntityType.Value))
        {
            return false;
        }

        return true;
    }

    private static bool IssueBelongsToEntityType(DiagnosticIssue issue, DiagnosticEntityType entityType) => entityType switch
    {
        DiagnosticEntityType.IndexedFile => issue is DiagnosticIssue.OrphanFile or DiagnosticIssue.UnidentifiedFile
            or DiagnosticIssue.MissingFileMetadata or DiagnosticIssue.MissingHlsSegments or DiagnosticIssue.MissingChapters,
        DiagnosticEntityType.Media => issue is DiagnosticIssue.MissingPictures or DiagnosticIssue.MissingExternalId
            or DiagnosticIssue.MissingMetadata or DiagnosticIssue.MissingFiles or DiagnosticIssue.StaleMetadata
            or DiagnosticIssue.MissingAudioAnalysis,
        DiagnosticEntityType.Library => issue is DiagnosticIssue.InaccessiblePath,
        _ => false
    };

    private static int CountIssue(LibraryHealthSummaryDto summary, DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.OrphanFile => summary.OrphanIndexedFileCount,
        DiagnosticIssue.UnidentifiedFile => summary.UnidentifiedIndexedFileCount,
        DiagnosticIssue.MissingFileMetadata => summary.MissingFileMetadataCount,
        DiagnosticIssue.MissingHlsSegments => summary.MissingHlsSegmentsCount,
        DiagnosticIssue.MissingChapters => summary.MissingChaptersCount,
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
            + summary.MissingFileMetadataCount + summary.MissingHlsSegmentsCount + summary.MissingChaptersCount,
        DiagnosticEntityType.Media => summary.MediaMissingPicturesCount + summary.MediaMissingExternalIdCount
            + summary.MediaMissingMetadataCount + summary.MediaWithoutFilesCount + summary.StaleMetadataCount
            + summary.MissingAudioAnalysisCount,
        DiagnosticEntityType.Library => summary.InaccessiblePathCount,
        _ => 0
    };
}
