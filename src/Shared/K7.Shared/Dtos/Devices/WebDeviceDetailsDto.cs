using K7.Server.Domain.Entities.Devices;
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

    public static WebDeviceDetailsDto? FromDomain(WebDeviceDetails? domain)
    {
        if (domain is null)
        {
            return null;
        }

        return new()
        {
            Browser = domain.Browser,
            RawUserAgent = domain.RawUserAgent,
            RawBrowserName = domain.RawBrowserName,
            RawBrowserVersion = domain.RawBrowserVersion,
            RawOperatingSystemName = domain.RawOperatingSystemName,
            RawOperatingSystemVersion = domain.RawOperatingSystemVersion,
            RawOperatingSystemVersionName = domain.RawOperatingSystemVersionName,
            RawPlatformType = domain.RawPlatformType,
            RawEngineName = domain.RawEngineName,
            RawEngineVersion = domain.RawEngineVersion
        };
    }
}
