using K7.Clients.MAUI.Interfaces;
using K7.Clients.Shared.Domain.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos.Devices;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using DeviceType = K7.Server.Domain.Enums.DeviceType;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Clients.MAUI.Services
{
    public class DeviceService(ICodecService codecHelper, IDeviceIdService deviceIdService, IDeviceStorageService deviceStorageService) : IDeviceService
    {
        public async Task<CreateDeviceRequest> GenerateCreateDeviceRequestAsync()
        {
            var supportedMediaFormats = await GetSupportedMediaFormatsAsync();
            var nativeDeviceDetails = await GetNativeDeviceDetailsAsync();

            return new CreateDeviceRequest
            {
                DeviceUniqueId = null,
                DeviceName = DeviceInfo.Name,
                ClientType = GetClientType(),
                DeviceType = await GetDeviceTypeAsync(),
                OperatingSystem = await GetOperatingSystemAsync(),
                OperatingSystemVersion = nativeDeviceDetails.RawVersion,
                DisplayHeight = DeviceDisplay.MainDisplayInfo.Orientation == DisplayOrientation.Landscape ? DeviceDisplay.MainDisplayInfo.Height : DeviceDisplay.MainDisplayInfo.Width,
                DisplayWidth = DeviceDisplay.MainDisplayInfo.Orientation == DisplayOrientation.Landscape ? DeviceDisplay.MainDisplayInfo.Width : DeviceDisplay.MainDisplayInfo.Height,
                NativeDeviceDetails = nativeDeviceDetails,
                WebDeviceDetails = null,
                PlaybackCapabilities = new CreateDeviceRequestPlaybackCapibilities()
                {
                    SupportedMediaFormatIds = supportedMediaFormats.Select(x => x.Id),
                    SupportedSubtitlesCodecs = null, // TODO
                    SupportsHDR = await GetHdrSupportAsync()
                },
                Users = [] // TODO - Current user
            };
        }

        public string? GetDeviceId()
        {
            return deviceStorageService.Get(PreferenceKeys.DEVICE_ID);
        }

        public string? GetDeviceUniqueId()
        {
            return deviceIdService.GetDeviceId();
        }

        public ClientType GetClientType()
        {
            return ClientType.Native;
        }

        public Task<DeviceType> GetDeviceTypeAsync()
        {
            return Task.FromResult(MapDeviceType(DeviceInfo.Idiom));
        }

        public Task<OperatingSystem> GetOperatingSystemAsync()
        {
            return Task.FromResult(MapOperatingSystem(DeviceInfo.Platform));
        }

        public Task<List<MediaFormatDto>> GetSupportedMediaFormatsAsync()
        {
            return Task.FromResult(new List<MediaFormatDto>()); // TODO
        }

        public Task<bool> GetHdrSupportAsync()
        {
            return codecHelper.GetHdrSupportAsync();
        }

        public Task<NativeDeviceDetailsDto> GetNativeDeviceDetailsAsync()
        {
            var details = new NativeDeviceDetailsDto()
            {
                RawDeviceType = DeviceInfo.DeviceType.ToString(),
                RawIdiom = DeviceInfo.Idiom.ToString(),
                RawManufacturer = DeviceInfo.Manufacturer,
                RawModel = DeviceInfo.Model,
                RawName = DeviceInfo.Name,
                RawPlatform = DeviceInfo.Platform.ToString(),
                RawVersion = DeviceInfo.VersionString
            };
            return Task.FromResult(details);
        }

        public Task<WebDeviceDetailsDto> GetWebDeviceDetailsAsync()
        {
            throw new InvalidOperationException($"Cannot fetch {nameof(WebDeviceDetailsDto)} from MAUI device.");
        }

        private static DeviceType MapDeviceType(DeviceIdiom deviceIdiom)
        {
            return deviceIdiom switch
            {
                var idiom when idiom.Equals(DeviceIdiom.Desktop) => DeviceType.Desktop,
                var idiom when idiom.Equals(DeviceIdiom.Phone) => DeviceType.Phone,
                var idiom when idiom.Equals(DeviceIdiom.Tablet) => DeviceType.Tablet,
                var idiom when idiom.Equals(DeviceIdiom.TV) => DeviceType.TV,
                var idiom when idiom.Equals(DeviceIdiom.Watch) => DeviceType.Watch,
                _ => DeviceType.Unknown,
            };
        }

        private static OperatingSystem MapOperatingSystem(DevicePlatform devicePlatform)
        {
            return devicePlatform switch
            {
                var platform when platform.Equals(DevicePlatform.Android) => OperatingSystem.Android,
                var platform when platform.Equals(DevicePlatform.iOS) => OperatingSystem.iOS,
                var platform when platform.Equals(DevicePlatform.MacCatalyst) => OperatingSystem.MacCatalyst,
                var platform when platform.Equals(DevicePlatform.WinUI) => OperatingSystem.Windows,
                var platform when platform.Equals(DevicePlatform.macOS) => OperatingSystem.Unknown,
                var platform when platform.Equals(DevicePlatform.Tizen) => OperatingSystem.Unknown,
                var platform when platform.Equals(DevicePlatform.tvOS) => OperatingSystem.Unknown,
                var platform when platform.Equals(DevicePlatform.watchOS) => OperatingSystem.Unknown,
                _ => OperatingSystem.Unknown
            };
        }
    }
}
