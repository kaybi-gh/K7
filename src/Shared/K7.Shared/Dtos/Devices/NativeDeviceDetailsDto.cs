using K7.Server.Domain.Entities.Devices;

namespace K7.Shared.Dtos.Devices;

public sealed record NativeDeviceDetailsDto
{
    public string? RawModel { get; init; }
    public string? RawManufacturer { get; init; }
    public string? RawName { get; init; }
    public string? RawVersion { get; init; }
    public string? RawPlatform { get; init; }
    public string? RawIdiom { get; init; }
    public string? RawDeviceType { get; init; }

    public static NativeDeviceDetailsDto? FromDomain(NativeDeviceDetails? domain)
    {
        if (domain is null)
        {
            return null;
        }

        return new()
        {
            RawModel = domain.RawModel,
            RawManufacturer = domain.RawManufacturer,
            RawName = domain.RawName,
            RawVersion = domain.RawVersion,
            RawPlatform = domain.RawPlatform,
            RawIdiom = domain.RawIdiom,
            RawDeviceType = domain.RawDeviceType
        };
    }
}
