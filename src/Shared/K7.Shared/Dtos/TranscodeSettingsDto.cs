using K7.Shared.Enums;

namespace K7.Shared.Dtos;

public sealed record TranscodeSettingsDto
{
    public HardwareEncoderMode EncoderMode { get; set; } = HardwareEncoderMode.Auto;
    public bool EnableHdrTonemap { get; set; } = true;
    public int MaxConcurrentTranscodes { get; set; }
    public int TranscodeTempQuotaMb { get; set; }
    public int EncoderThrottleBufferSegments { get; set; } = 3;
}
