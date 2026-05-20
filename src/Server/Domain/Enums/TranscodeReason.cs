namespace K7.Server.Domain.Enums;

[Flags]
public enum TranscodeReason
{
    None = 0,
    VideoCodecNotSupported = 1 << 0,
    AudioCodecNotSupported = 1 << 1,
    ContainerNotSupported = 1 << 2,
    HlsSegmentsUnavailable = 1 << 3,
    SubtitlesBurnIn = 1 << 4,
    ResolutionNotSupported = 1 << 5
}
