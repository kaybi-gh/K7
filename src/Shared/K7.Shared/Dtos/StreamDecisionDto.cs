using K7.Server.Domain.Enums;
using K7.Shared.Enums;

namespace K7.Shared.Dtos;

public sealed record StreamDecisionDto
{
    public PlaybackMode Mode { get; init; }
    public TranscodeReason Reason { get; init; }
    public string? SourceVideoCodec { get; init; }
    public string? SourceAudioCodec { get; init; }
    public string? StreamVideoCodec { get; init; }
    public string? StreamAudioCodec { get; init; }
    public string? SourceResolution { get; init; }
    public int? SelectedAudioTrackIndex { get; init; }
    public int? Bitrate { get; init; }
    public string? AudioTrackLanguage { get; init; }
    public string? AudioTrackTitle { get; init; }
    public string? AudioChannelLayout { get; init; }
    public string? SubtitleTrackLanguage { get; init; }
    public string? SubtitleTrackTitle { get; init; }
    public string? SubtitleCodec { get; init; }
    public int? SelectedSubtitleTrackIndex { get; init; }
    public bool IsSubtitleBurnIn { get; init; }
    public string? VideoEncoder { get; init; }
    public bool? IsHardwareAccelerated { get; init; }
}
