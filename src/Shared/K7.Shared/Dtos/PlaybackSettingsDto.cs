namespace K7.Shared.Dtos;

public class PlaybackSettingsDto
{
    public int AudioTrackIndex { get; set; }
    public int? SubtitleTrackIndex { get; set; }
    public int Quality { get; set; }
    /// <summary>
    /// HDMI bitstream for this session. Device-local; not a user/server preference.
    /// </summary>
    public bool AudioPassthrough { get; set; } = true;
}

