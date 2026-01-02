using K7.Clients.MAUI.Interfaces;
using K7.Clients.Shared.Domain.Interfaces;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using DeviceType = K7.Server.Domain.Enums.DeviceType;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Clients.MAUI.Services
{
    public class DeviceService : IDeviceService
    {
        private readonly ICodecService _codecHelper;
        private readonly IDeviceIdService _deviceIdService;

        public DeviceService(ICodecService codecHelper, IDeviceIdService deviceIdService)
        {
            _codecHelper = codecHelper;
            _deviceIdService = deviceIdService;
        }

        public async Task<CreateDeviceRequest> GenerateCreateDeviceRequestAsync()
        {
            var supportedMediaFormats = await GetSupportedMediaFormatsAsync();

            return new CreateDeviceRequest
            {
                DeviceName = DeviceInfo.Name,
                DeviceUniqueId = GetDeviceId(),
                DeviceType = await GetDeviceTypeAsync(),
                OperatingSystemVersion = DeviceInfo.VersionString,
                OperatingSystem = GetOperatingSystem(),
                DisplayHeight = DeviceDisplay.MainDisplayInfo.Orientation == DisplayOrientation.Landscape ? DeviceDisplay.MainDisplayInfo.Height : DeviceDisplay.MainDisplayInfo.Width,
                DisplayWidth = DeviceDisplay.MainDisplayInfo.Orientation == DisplayOrientation.Landscape ? DeviceDisplay.MainDisplayInfo.Width : DeviceDisplay.MainDisplayInfo.Height,
                SupportedMediaFormatIds = supportedMediaFormats.Select(x => x.Id),
                SupportsHDR = await GetHdrSupportAsync()
            };
        }

        public OperatingSystem GetOperatingSystem()
        {
            return DeviceInfo.Platform switch
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

        public string? GetDeviceId()
        {
            return _deviceIdService.GetDeviceId();
        }

        public Task<DeviceType> GetDeviceTypeAsync()
        {
            var type = DeviceInfo.Idiom switch
            {
                var idiom when idiom.Equals(DeviceIdiom.Desktop) => DeviceType.Desktop,
                var idiom when idiom.Equals(DeviceIdiom.Phone) => DeviceType.Phone,
                var idiom when idiom.Equals(DeviceIdiom.Tablet) => DeviceType.Tablet,
                var idiom when idiom.Equals(DeviceIdiom.TV) => DeviceType.TV,
                var idiom when idiom.Equals(DeviceIdiom.Watch) => DeviceType.Watch,
                _ => DeviceType.Unknown,
            };
            return Task.FromResult(type);
        }

        public string GetPlatform()
        {
            return DeviceInfo.Platform.ToString() + " - " + DeviceInfo.VersionString + " - " + DeviceInfo.Name + " - " + DeviceInfo.Manufacturer + " - " + DeviceInfo.DeviceType.ToString() + " - " + DeviceInfo.Model;
        }

        public Task<List<MediaFormatDto>> GetSupportedMediaFormatsAsync()
        {
            return Task.FromResult(new List<MediaFormatDto>()); // TODO
        }

        public Task<bool> GetHdrSupportAsync()
        {
            return _codecHelper.GetHdrSupportAsync();
        }
    }
}
