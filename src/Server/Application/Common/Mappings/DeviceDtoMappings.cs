using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Devices;
using K7.Shared.Dtos.Devices;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Users;

namespace K7.Server.Application.Common.Mappings;

public static class DeviceDtoMappings
{
    extension(Device domain)
    {
        public DeviceDto ToDeviceDto() => new()
        {
            Id = domain.Id,
            DeviceUniqueId = domain.DeviceUniqueId,
            DeviceName = domain.DeviceName,
            ClientType = domain.ClientType,
            DeviceType = domain.DeviceType,
            OperatingSystem = domain.OperatingSystem,
            OperatingSystemVersion = domain.OperatingSystemVersion,
            DisplayHeight = domain.DisplayHeight,
            DisplayWidth = domain.DisplayWidth,
            NativeDeviceDetails = domain.NativeDeviceDetails.ToNativeDeviceDetailsDto(),
            WebDeviceDetails = domain.WebDeviceDetails.ToWebDeviceDetailsDto(),
            PlaybackCapabilities = domain.PlaybackCapabilities.ToDevicePlaybackCapabilitiesDto(),
            Users = domain.Users.Select(u => u.ToLiteUserDto()).ToList(),
            LastSeen = domain.LastSeen
        };
    }

    extension(DevicePlaybackCapabilities domain)
    {
        public DevicePlaybackCapabilitiesDto ToDevicePlaybackCapabilitiesDto() => new()
        {
            SupportedMediaFormats = Constants.MediaFormats
                .Where(x => domain.SupportedMediaFormatIds.Contains(x.Id))
                .Select(f => f.ToMediaFormatDto())
                .ToList(),
            SupportedSubtitlesCodecs = domain.SupportedSubtitlesCodecs.ToList(),
            SupportsHDR = domain.SupportsHDR,
        };
    }

    extension(WebDeviceDetails? domain)
    {
        public WebDeviceDetailsDto? ToWebDeviceDetailsDto()
        {
            if (domain is null)
                return null;

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

    extension(NativeDeviceDetails? domain)
    {
        public NativeDeviceDetailsDto? ToNativeDeviceDetailsDto()
        {
            if (domain is null)
                return null;

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
}
