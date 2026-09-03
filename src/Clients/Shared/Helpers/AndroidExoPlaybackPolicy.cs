namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Android ExoPlayer hitch policy. HDMI tunneling stays off on every device, including
/// Amlogic (Nokia Streaming Box): tunneling plus EAC3 Direct Play can throw ExoPlayer
/// ERROR_CODE_FAILED_RUNTIME_CHECK (1004) depending on HDMI sink and firmware, so two
/// identical boxes disagree.
/// </summary>
public static class AndroidExoPlaybackPolicy
{
    public static bool IsAmlogicDevice(string? manufacturer, string? model)
    {
        var maker = manufacturer ?? "";
        var name = model ?? "";
        return maker.Contains("SEI", StringComparison.OrdinalIgnoreCase)
            || maker.Contains("Amlogic", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Streaming Box", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Amlogic", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Media3 tunneling stays off. Amlogic AudioTrack HW_AV_SYNC + EAC3 5.1 Direct Play
    /// is firmware/sink dependent (1004 at t=0). Tegra hitching is a separate reason to
    /// keep this false on Shield. Keep the hook so a future opt-in can exist.
    /// </summary>
    public static bool ShouldEnableHdmiTunneling(string? manufacturer, string? model)
    {
        _ = manufacturer;
        _ = model;
        return false;
    }

    /// <summary>
    /// Scale on TV: pick the 1x/2x/2.5x HDMI size closest to the file.
    /// SCALE_ON_DEVICE keeps the current panel size (usually 4K).
    /// </summary>
    public static bool ShouldPreferContentHdmiResolution(HdmiAutoFrameRateMode mode) =>
        mode == HdmiAutoFrameRateMode.ScaleOnTv;

    /// <summary>
    /// After an app HDMI mode switch, pin Amlogic HAL AFR off so it cannot retime.
    /// Do not pin when app AFR is disabled: leaving the HAL default
    /// and is smoother than policy=0 at 59.94.
    /// Restore policy 2 when leaving the player.
    /// </summary>
    public static bool ShouldDisableVendorVideoAfr(string? manufacturer, string? model) =>
        IsAmlogicDevice(manufacturer, model);

    /// <summary>
    /// HDMI auto frame rate (window preferredDisplayModeId) on Android TV when
    /// the user did not pick Disabled.
    /// </summary>
    public static bool ShouldApplyHdmiAutoFrameRate(
        HdmiAutoFrameRateMode mode,
        bool isTelevision,
        string? manufacturer,
        string? model) =>
        mode != HdmiAutoFrameRateMode.Disabled
        && (isTelevision || IsAmlogicDevice(manufacturer, model));

    /// <summary>
    /// Surface.setFrameRate only when app AFR has switched HDMI to the file rate.
    /// With AFR off the panel stays 59.94; Exo OnlyIfSeamless still pokes the surface
    /// and Amlogic HAL AFR retimes. Phones stay off.
    /// </summary>
    public static bool ShouldAllowSurfaceFrameRateChanges(
        HdmiAutoFrameRateMode afrMode,
        bool isTelevision,
        string? manufacturer,
        string? model) =>
        afrMode != HdmiAutoFrameRateMode.Disabled
        && (isTelevision || IsAmlogicDevice(manufacturer, model));

    /// <summary>
    /// Enable Media3 audio offload on TV (bitstream to the DSP).
    /// K7 used to force it off; that left EAC3 on a MediaCodec clock that hitchs vs HDMI.
    /// Tunneling stays off (EAC3 1004).
    /// </summary>
    public static bool ShouldEnableAudioOffload(
        bool isTelevision,
        string? manufacturer,
        string? model) =>
        isTelevision || IsAmlogicDevice(manufacturer, model);

    /// <summary>
    /// Compressed audio offload bypasses the Sonic time-stretch processor, so playback speed
    /// other than 1x is a no-op on an offloaded Direct Play track. Keep offload only at normal
    /// speed; disable it while speeding so the decoded PCM + Sonic path applies the rate.
    /// </summary>
    public static bool ShouldEnableAudioOffloadForSpeed(bool policyOffloadEnabled, double speed) =>
        policyOffloadEnabled && Math.Abs(speed - 1.0) < 0.01;

    /// <summary>
    /// Profile 8.1: query HEVC decoders instead of video/dolby-vision. The file is
    /// unchanged; MediaCodec gets the HDR10 base layer. Native keeps the DV MIME path.
    /// </summary>
    public static bool ShouldPreferHevcDecoderForDolbyVision(DolbyVisionDecodeMode mode) =>
        mode == DolbyVisionDecodeMode.HevcHdr10;

    /// <summary>
    /// Custom ExoPlayer (offload, decoder fallback, optional DV-as-HEVC) on Android TV
    /// and on Amlogic boxes that report a phone/tablet idiom.
    /// </summary>
    public static bool ShouldInstallTunedExoPlayer(
        bool isTelevision,
        string? manufacturer,
        string? model) =>
        isTelevision || IsAmlogicDevice(manufacturer, model);

    public static bool IsPreferredVendorDecoder(string? codecName)
    {
        if (string.IsNullOrEmpty(codecName))
            return false;

        if (codecName.Contains("google", StringComparison.OrdinalIgnoreCase)
            || codecName.Contains("c2.android.", StringComparison.OrdinalIgnoreCase)
            || codecName.Contains("android.hevc", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (codecName.Contains("amlogic", StringComparison.OrdinalIgnoreCase)
            || codecName.Contains("nvidia", StringComparison.OrdinalIgnoreCase)
            || codecName.Contains("tegra", StringComparison.OrdinalIgnoreCase)
            || codecName.Contains("mediatek", StringComparison.OrdinalIgnoreCase)
            || codecName.Contains("mtk", StringComparison.OrdinalIgnoreCase)
            || codecName.Contains("qti", StringComparison.OrdinalIgnoreCase)
            || codecName.Contains("qcom", StringComparison.OrdinalIgnoreCase)
            || codecName.Contains("exynos", StringComparison.OrdinalIgnoreCase)
            || codecName.Contains("broadcom", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (codecName.StartsWith("OMX.", StringComparison.Ordinal))
            return true;

        return codecName.StartsWith("c2.", StringComparison.OrdinalIgnoreCase);
    }
}
