namespace K7.Server.Domain.Entities.Devices;

public class WebDeviceDetails
{
    public Browser Browser { get; set; }
    public string? RawUserAgent { get; set; }
    public string? RawBrowserName { get; set; }
    public string? RawBrowserVersion { get; set; }
    public string? RawOperatingSystemName { get; set; }
    public string? RawOperatingSystemVersion { get; set; }
    public string? RawOperatingSystemVersionName { get; set; }
    public string? RawPlatformType { get; set; }
    public string? RawEngineName { get; set; }
    public string? RawEngineVersion { get; set; }
}
