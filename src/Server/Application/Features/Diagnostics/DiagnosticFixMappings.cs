using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Diagnostics;

public static class DiagnosticFixMappings
{
    public static DiagnosticFixAction? GetFixAction(DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.MissingExternalId => DiagnosticFixAction.AutoReidentifyMetadata,
        DiagnosticIssue.MissingPictures or DiagnosticIssue.MissingMetadata or DiagnosticIssue.StaleMetadata
            or DiagnosticIssue.MissingMembers => DiagnosticFixAction.RefreshMetadata,
        DiagnosticIssue.MissingAudioAnalysis => DiagnosticFixAction.AnalyzeMusicTrackAudio,
        DiagnosticIssue.MissingFileMetadata => DiagnosticFixAction.ExtractFileMetadata,
        DiagnosticIssue.MissingHlsSegments => DiagnosticFixAction.ComputeHlsSegments,
        DiagnosticIssue.OrphanFile => DiagnosticFixAction.RetryCreateMedia,
        _ => null
    };

    public static bool SupportsBulkFix(DiagnosticIssue issue) => GetFixAction(issue) is not null;
}
