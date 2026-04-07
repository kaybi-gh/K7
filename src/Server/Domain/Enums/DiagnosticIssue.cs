namespace K7.Server.Domain.Enums;

public enum DiagnosticIssue
{
    OrphanFile,
    UnidentifiedFile,
    MissingFileMetadata,
    MissingHlsSegments,
    MissingPictures,
    MissingMetadata,
    StaleMetadata,
    MissingAudioAnalysis,
    MissingFiles
}
