namespace K7.Server.Domain.Entities.Users;

public class PlaybackSessionDetails : BaseEntity
{
    public Guid MediaPlaybackSessionId { get; set; }
    public MediaPlaybackSession MediaPlaybackSession { get; set; } = null!;

    public bool? IsTranscode { get; set; }
    public string? VideoDecision { get; set; }
    public string? AudioDecision { get; set; }
    public Enums.TranscodeReason? TranscodeReason { get; set; }
    public int? Bitrate { get; set; }
    public string? SourceVideoCodec { get; set; }
    public string? SourceAudioCodec { get; set; }
    public int? SourceVideoWidth { get; set; }
    public int? SourceVideoHeight { get; set; }
    public string? StreamVideoCodec { get; set; }
    public string? StreamAudioCodec { get; set; }
    public string? AudioTrackLanguage { get; set; }
    public string? AudioTrackTitle { get; set; }
    public string? AudioChannelLayout { get; set; }
    public string? SubtitleTrackLanguage { get; set; }
    public string? SubtitleTrackTitle { get; set; }
}
