namespace K7.Shared.Dtos;

public sealed record FfmpegCapabilitiesDto
{
    public string? FfmpegVersion { get; init; }
    public IReadOnlyList<string> HardwareAccelerators { get; init; } = [];
    public IReadOnlyList<string> VideoEncoders { get; init; } = [];
    public IReadOnlyList<string> AvailableHardwareEncoders { get; init; } = [];
    /// <summary>
    /// True when ffmpeg was built with the scale_cuda filter. Distro packages often
    /// ship NVENC without CUDA filters, so quality downscale then stays on the CPU.
    /// </summary>
    public bool CudaScaleFilterAvailable { get; init; }
}

public sealed record FfmpegTranscodeTestResultDto
{
    public bool Success { get; init; }
    public string? SelectedEncoder { get; init; }
    public bool IsHardwareAccelerated { get; init; }
    public string? Error { get; init; }
    public FfmpegCapabilitiesDto? Capabilities { get; init; }
}
