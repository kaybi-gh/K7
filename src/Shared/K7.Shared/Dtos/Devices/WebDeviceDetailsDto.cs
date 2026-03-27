using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Devices;

public sealed record WebDeviceDetailsDto
{
    public Browser Browser { get; init; }
    public string? RawUserAgent { get; init; }
    public string? RawBrowserName { get; init; }
    public string? RawBrowserVersion { get; init; }
    public string? RawOperatingSystemName { get; init; }
    public string? RawOperatingSystemVersion { get; init; }
    public string? RawOperatingSystemVersionName { get; init; }
    public string? RawPlatformType { get; init; }
    public string? RawEngineName { get; init; }
    public string? RawEngineVersion { get; init; }

}
