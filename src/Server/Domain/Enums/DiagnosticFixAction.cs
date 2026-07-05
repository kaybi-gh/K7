namespace K7.Server.Domain.Enums;

public enum DiagnosticFixAction
{
    RefreshMetadata = 1,
    ExtractFileMetadata = 2,
    ComputeHlsSegments = 3,
    AutoReidentifyMetadata = 4,
    AnalyzeMusicTrackAudio = 5,
    RetryCreateMedia = 6
}
