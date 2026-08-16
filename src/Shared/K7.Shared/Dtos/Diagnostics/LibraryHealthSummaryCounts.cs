using K7.Server.Domain.Enums;
using K7.Shared.Diagnostics;

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
    public static readonly DiagnosticIssue[] ErrorIssues = DiagnosticIssueTaxonomy.ErrorIssues;
    public static readonly DiagnosticIssue[] WarningIssues = DiagnosticIssueTaxonomy.WarningIssues;
    public static readonly DiagnosticIssue[] InfoIssues = DiagnosticIssueTaxonomy.InfoIssues;

    public static int SumErrors(IEnumerable<LibraryHealthSummaryDto> summaries) =>
        summaries.Sum(l => l.OrphanIndexedFileCount
            + l.MissingFileMetadataCount
            + l.MediaMissingExternalIdCount
            + l.InaccessiblePathCount);

    public static int SumWarnings(IEnumerable<LibraryHealthSummaryDto> summaries) =>
        summaries.Sum(l => l.DuplicateExternalIdCount);

    public static int SumInfo(IEnumerable<LibraryHealthSummaryDto> summaries) =>
        summaries.Sum(l => l.MissingHlsSegmentsCount
            + l.MissingChaptersCount
            + l.MissingThemeSongCount
            + l.MissingIntroOutroCount
            + l.MediaMissingPicturesCount
            + l.StaleMetadataCount
            + l.MissingAudioAnalysisCount
            + l.MissingMembersCount
            + l.SuspectedDuplicateMediaCount
            + l.MediaMissingMetadataCount);

    public static int SumTotal(IEnumerable<LibraryHealthSummaryDto> summaries) =>
        SumErrors(summaries) + SumWarnings(summaries) + SumInfo(summaries);

    public static int SumLibraryIssues(LibraryHealthSummaryDto summary) =>
        DiagnosticIssueTaxonomy.SurfacedIssues.Sum(issue => CountIssue(summary, issue));

    public static int SumIssue(IEnumerable<LibraryHealthSummaryDto> summaries, DiagnosticIssue issue) =>
        summaries.Sum(s => CountIssue(s, DiagnosticIssueTaxonomy.Canonicalize(issue)));

    public static int SumEntityType(IEnumerable<LibraryHealthSummaryDto> summaries, DiagnosticEntityType entityType) =>
        summaries.Sum(s => CountEntityType(s, entityType));

    public static int SumWorkClass(IEnumerable<LibraryHealthSummaryDto> summaries, DiagnosticWorkClass workClass) =>
        DiagnosticIssueTaxonomy.IssuesForWorkClass(workClass).Sum(issue => SumIssue(summaries, issue));

    public static int SumSeverity(
        IEnumerable<LibraryHealthSummaryDto> summaries,
        IReadOnlyCollection<DiagnosticIssue> severityIssues,
        DiagnosticsFilterContext context,
        DiagnosticsFilterExclusions exclusions)
    {
        var excludesSeverity = exclusions | DiagnosticsFilterExclusions.Severity;
        return severityIssues
            .Select(DiagnosticIssueTaxonomy.Canonicalize)
            .Distinct()
            .Sum(issue => SumIssue(summaries, issue, context, excludesSeverity));
    }

    public static int SumIssue(
        IEnumerable<LibraryHealthSummaryDto> summaries,
        DiagnosticIssue issue,
        DiagnosticsFilterContext context,
        DiagnosticsFilterExclusions exclusions)
    {
        issue = DiagnosticIssueTaxonomy.Canonicalize(issue);
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
        DiagnosticIssueTaxonomy.SurfacedIssues
            .Where(issue => IssueBelongsToEntityType(issue, entityType))
            .Sum(issue => SumIssue(summaries, issue, context, exclusions | DiagnosticsFilterExclusions.EntityType));

    public static int SumLibraryIssues(
        LibraryHealthSummaryDto summary,
        DiagnosticsFilterContext context,
        DiagnosticsFilterExclusions exclusions) =>
        DiagnosticIssueTaxonomy.SurfacedIssues
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
            && DiagnosticIssueTaxonomy.Canonicalize(context.Issue.Value) != issue)
        {
            return false;
        }

        if (!exclusions.HasFlag(DiagnosticsFilterExclusions.Severity)
            && context.SeverityIssues is { Count: > 0 }
            && !context.SeverityIssues.Select(DiagnosticIssueTaxonomy.Canonicalize).Contains(issue))
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

    private static bool IssueBelongsToEntityType(DiagnosticIssue issue, DiagnosticEntityType entityType) =>
        DiagnosticIssueTaxonomy.GetEntityType(issue) == entityType;

    private static int CountIssue(LibraryHealthSummaryDto summary, DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.OrphanFile => summary.OrphanIndexedFileCount,
        DiagnosticIssue.UnidentifiedFile => 0,
        DiagnosticIssue.MissingFileMetadata => summary.MissingFileMetadataCount,
        DiagnosticIssue.MissingHlsSegments => summary.MissingHlsSegmentsCount,
        DiagnosticIssue.MissingChapters => summary.MissingChaptersCount,
        DiagnosticIssue.MissingThemeSong => summary.MissingThemeSongCount,
        DiagnosticIssue.MissingIntroOutro => summary.MissingIntroOutroCount,
        DiagnosticIssue.MissingPictures => summary.MediaMissingPicturesCount,
        DiagnosticIssue.MissingMetadata => summary.MediaMissingMetadataCount,
        DiagnosticIssue.MissingExternalId => summary.MediaMissingExternalIdCount,
        DiagnosticIssue.StaleMetadata => summary.StaleMetadataCount,
        DiagnosticIssue.MissingAudioAnalysis => summary.MissingAudioAnalysisCount,
        DiagnosticIssue.MissingFiles => 0,
        DiagnosticIssue.MissingMembers => summary.MissingMembersCount,
        DiagnosticIssue.DuplicateExternalId => summary.DuplicateExternalIdCount,
        DiagnosticIssue.SuspectedDuplicateMedia => summary.SuspectedDuplicateMediaCount,
        DiagnosticIssue.InaccessiblePath => summary.InaccessiblePathCount,
        _ => 0
    };

    private static int CountEntityType(LibraryHealthSummaryDto summary, DiagnosticEntityType entityType) => entityType switch
    {
        DiagnosticEntityType.IndexedFile => summary.OrphanIndexedFileCount
            + summary.MissingFileMetadataCount
            + summary.MissingHlsSegmentsCount
            + summary.MissingChaptersCount,
        DiagnosticEntityType.Media => summary.MediaMissingPicturesCount
            + summary.MediaMissingExternalIdCount
            + summary.MediaMissingMetadataCount
            + summary.StaleMetadataCount
            + summary.MissingAudioAnalysisCount
            + summary.MissingThemeSongCount
            + summary.MissingIntroOutroCount
            + summary.DuplicateExternalIdCount
            + summary.SuspectedDuplicateMediaCount
            + summary.MissingMembersCount,
        DiagnosticEntityType.Library => summary.InaccessiblePathCount,
        _ => 0
    };
}
