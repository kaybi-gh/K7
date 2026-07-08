namespace K7.Shared.Dtos;

public sealed record AudioPlayerSettingsDto
{
    public bool LoudnessEnabled { get; set; } = true;
    public double LoudnessTargetLufs { get; set; } = -14.0;
    public double LoudnessPreampDb { get; set; } = 0.0;
    public bool LimiterEnabled { get; set; } = true;
    public bool EqEnabled { get; set; }
    public string EqPresetName { get; set; } = "flat";
    public double[] EqBands { get; set; } = new double[10];
    public double CrossfadeDuration { get; set; } = 6.0;
    public bool AdaptiveCrossfade { get; set; } = true;
    public bool AutoplayEnabled { get; set; } = true;
    public int StreamingQualityWifi { get; set; }
    public int StreamingQualityMobile { get; set; }
    public bool DownmixToStereo { get; set; }
    public bool ShowFullscreenOnPlay { get; set; }
    public bool KeepScreenOn { get; set; }
    public int SkipBackSeconds { get; set; } = 5;
    public int SkipForwardSeconds { get; set; } = 5;
}
