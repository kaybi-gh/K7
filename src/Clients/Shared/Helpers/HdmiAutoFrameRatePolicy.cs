namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Device-local HDMI auto frame rate, three choices:
/// off, SCALE_ON_DEVICE (keep panel size), SCALE_ON_TV (HDMI size closest to the file).
/// </summary>
public enum HdmiAutoFrameRateMode
{
    Disabled,
    ScaleOnDevice,
    ScaleOnTv
}

public static class HdmiAutoFrameRatePolicy
{
    public const string Disabled = "disabled";
    public const string ScaleOnDevice = "device";
    public const string ScaleOnTv = "tv";

    public static HdmiAutoFrameRateMode? TryParse(string? stored) => stored?.Trim().ToLowerInvariant() switch
    {
        Disabled => HdmiAutoFrameRateMode.Disabled,
        ScaleOnDevice => HdmiAutoFrameRateMode.ScaleOnDevice,
        ScaleOnTv => HdmiAutoFrameRateMode.ScaleOnTv,
        _ => null
    };

    public static string Persist(HdmiAutoFrameRateMode mode) => mode switch
    {
        HdmiAutoFrameRateMode.ScaleOnDevice => ScaleOnDevice,
        HdmiAutoFrameRateMode.ScaleOnTv => ScaleOnTv,
        _ => Disabled
    };

    /// <summary>
    /// Amlogic (Nokia) defaults to off: 24 Hz HDMI on that HAL can hitch more than
    /// 23.976 on 59.94. Other Android TV keeps SCALE_ON_DEVICE (previous K7 behavior).
    /// </summary>
    public static HdmiAutoFrameRateMode DefaultForDevice(
        bool isTelevision,
        string? manufacturer,
        string? model)
    {
        if (AndroidExoPlaybackPolicy.IsAmlogicDevice(manufacturer, model))
            return HdmiAutoFrameRateMode.Disabled;

        return isTelevision
            ? HdmiAutoFrameRateMode.ScaleOnDevice
            : HdmiAutoFrameRateMode.Disabled;
    }

    public static HdmiAutoFrameRateMode Resolve(
        string? stored,
        bool isTelevision,
        string? manufacturer,
        string? model) =>
        TryParse(stored) ?? DefaultForDevice(isTelevision, manufacturer, model);
}
