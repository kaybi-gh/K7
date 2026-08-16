using K7.Server.Domain.Enums;

namespace K7.Shared.Diagnostics;

/// <summary>
/// Single source of truth for diagnostic issue severity, work class, and UI surfacing.
/// </summary>
public static class DiagnosticIssueTaxonomy
{
    public static readonly DiagnosticIssue[] SurfacedIssues =
    [
        DiagnosticIssue.OrphanFile,
        DiagnosticIssue.MissingFileMetadata,
        DiagnosticIssue.InaccessiblePath,
        DiagnosticIssue.DuplicateExternalId,
        DiagnosticIssue.SuspectedDuplicateMedia,
        DiagnosticIssue.MissingExternalId,
        DiagnosticIssue.MissingMetadata,
        DiagnosticIssue.MissingPictures,
        DiagnosticIssue.StaleMetadata,
        DiagnosticIssue.MissingMembers,
        DiagnosticIssue.MissingHlsSegments,
        DiagnosticIssue.MissingChapters,
        DiagnosticIssue.MissingThemeSong,
        DiagnosticIssue.MissingIntroOutro,
        DiagnosticIssue.MissingAudioAnalysis
    ];

    public static readonly DiagnosticIssue[] ErrorIssues =
    [
        DiagnosticIssue.OrphanFile,
        DiagnosticIssue.MissingFileMetadata,
        DiagnosticIssue.MissingExternalId,
        DiagnosticIssue.InaccessiblePath
    ];

    public static readonly DiagnosticIssue[] WarningIssues =
    [
        DiagnosticIssue.DuplicateExternalId
    ];

    public static readonly DiagnosticIssue[] InfoIssues =
    [
        DiagnosticIssue.MissingHlsSegments,
        DiagnosticIssue.MissingChapters,
        DiagnosticIssue.MissingThemeSong,
        DiagnosticIssue.MissingIntroOutro,
        DiagnosticIssue.MissingPictures,
        DiagnosticIssue.StaleMetadata,
        DiagnosticIssue.MissingAudioAnalysis,
        DiagnosticIssue.MissingMembers,
        DiagnosticIssue.SuspectedDuplicateMedia,
        DiagnosticIssue.MissingMetadata
    ];

    public static bool IsSurfaced(DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.MissingFiles or DiagnosticIssue.UnidentifiedFile => false,
        _ => true
    };

    /// <summary>
    /// Maps legacy / aliased filters onto the canonical surfaced issue.
    /// </summary>
    public static DiagnosticIssue Canonicalize(DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.UnidentifiedFile => DiagnosticIssue.OrphanFile,
        _ => issue
    };

    public static DiagnosticWorkClass GetWorkClass(DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.OrphanFile
            or DiagnosticIssue.UnidentifiedFile
            or DiagnosticIssue.MissingFileMetadata
            or DiagnosticIssue.InaccessiblePath
            or DiagnosticIssue.DuplicateExternalId
            or DiagnosticIssue.SuspectedDuplicateMedia
            => DiagnosticWorkClass.Catalog,

        DiagnosticIssue.MissingExternalId
            or DiagnosticIssue.MissingMetadata
            or DiagnosticIssue.MissingPictures
            or DiagnosticIssue.StaleMetadata
            or DiagnosticIssue.MissingMembers
            => DiagnosticWorkClass.Enrichment,

        DiagnosticIssue.MissingHlsSegments
            or DiagnosticIssue.MissingChapters
            or DiagnosticIssue.MissingThemeSong
            or DiagnosticIssue.MissingIntroOutro
            or DiagnosticIssue.MissingAudioAnalysis
            => DiagnosticWorkClass.Polish,

        // Hidden from UI; keep a stable bucket if referenced.
        DiagnosticIssue.MissingFiles => DiagnosticWorkClass.Catalog,
        _ => DiagnosticWorkClass.Catalog
    };

    /// <summary>
    /// Default severity for an issue type. OrphanFile (unlinked file) is always Error.
    /// </summary>
    public static DiagnosticSeverity GetSeverity(DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.OrphanFile => DiagnosticSeverity.Error,
        DiagnosticIssue.UnidentifiedFile => DiagnosticSeverity.Error,
        DiagnosticIssue.MissingFileMetadata => DiagnosticSeverity.Error,
        DiagnosticIssue.MissingExternalId => DiagnosticSeverity.Error,
        DiagnosticIssue.MissingFiles => DiagnosticSeverity.Error,
        DiagnosticIssue.InaccessiblePath => DiagnosticSeverity.Error,

        DiagnosticIssue.DuplicateExternalId => DiagnosticSeverity.Warning,

        DiagnosticIssue.MissingHlsSegments
            or DiagnosticIssue.MissingChapters
            or DiagnosticIssue.MissingThemeSong
            or DiagnosticIssue.MissingIntroOutro
            or DiagnosticIssue.MissingPictures
            or DiagnosticIssue.StaleMetadata
            or DiagnosticIssue.MissingAudioAnalysis
            or DiagnosticIssue.MissingMembers
            or DiagnosticIssue.SuspectedDuplicateMedia
            or DiagnosticIssue.MissingMetadata
            => DiagnosticSeverity.Info,

        _ => DiagnosticSeverity.Warning
    };

    /// <summary>
    /// Unlinked files are always Error (identified or not). Kept for callers that still pass identification.
    /// </summary>
    public static DiagnosticSeverity GetOrphanRowSeverity(bool hasIdentification) =>
        DiagnosticSeverity.Error;

    public static DiagnosticEntityType GetEntityType(DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.OrphanFile
            or DiagnosticIssue.UnidentifiedFile
            or DiagnosticIssue.MissingFileMetadata
            or DiagnosticIssue.MissingHlsSegments
            or DiagnosticIssue.MissingChapters
            => DiagnosticEntityType.IndexedFile,

        DiagnosticIssue.InaccessiblePath => DiagnosticEntityType.Library,

        _ => DiagnosticEntityType.Media
    };

    public static IReadOnlyList<DiagnosticIssue> IssuesForWorkClass(DiagnosticWorkClass workClass) =>
        SurfacedIssues.Where(i => GetWorkClass(i) == workClass).ToArray();

    public static IReadOnlyCollection<DiagnosticIssue> IssuesForSeverityFilter(string? severity) => severity switch
    {
        "error" => ErrorIssues,
        "warning" => WarningIssues,
        "info" => InfoIssues,
        _ => SurfacedIssues
    };

    public static bool SupportsBulkFix(DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.OrphanFile
            or DiagnosticIssue.MissingFileMetadata
            or DiagnosticIssue.MissingExternalId
            or DiagnosticIssue.MissingMetadata
            or DiagnosticIssue.MissingPictures
            or DiagnosticIssue.StaleMetadata
            or DiagnosticIssue.MissingMembers
            or DiagnosticIssue.MissingHlsSegments
            or DiagnosticIssue.MissingChapters
            or DiagnosticIssue.MissingThemeSong
            or DiagnosticIssue.MissingIntroOutro
            or DiagnosticIssue.MissingAudioAnalysis
            => true,
        _ => false
    };
}
