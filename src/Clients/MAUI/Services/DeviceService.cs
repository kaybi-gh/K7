using K7.Clients.MAUI.Interfaces;
using K7.Clients.Shared.Domain.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Interfaces;
using K7.Shared.Dtos.Devices;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using DeviceType = K7.Server.Domain.Enums.DeviceType;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Clients.MAUI.Services
{
    public class DeviceService(ICodecService codecHelper, IDeviceIdService deviceIdService, IDeviceStorageService deviceStorageService, IK7ServerService k7ServerService) : IDeviceService
    {
        public async Task<CreateDeviceRequest> GenerateCreateDeviceRequestAsync()
        {
            var supportedMediaFormats = await GetSupportedMediaFormatsAsync();
            var nativeDeviceDetails = await GetNativeDeviceDetailsAsync();
            var displayInfo = await MainThread.InvokeOnMainThreadAsync(() => DeviceDisplay.MainDisplayInfo);

            return new CreateDeviceRequest
            {
                DeviceUniqueId = deviceIdService.GetDeviceId(),
                DeviceName = DeviceInfo.Name,
                ClientType = GetClientType(),
                DeviceType = await GetDeviceTypeAsync(),
                OperatingSystem = await GetOperatingSystemAsync(),
                OperatingSystemVersion = nativeDeviceDetails.RawVersion,
                DisplayHeight = displayInfo.Orientation == DisplayOrientation.Landscape ? displayInfo.Height : displayInfo.Width,
                DisplayWidth = displayInfo.Orientation == DisplayOrientation.Landscape ? displayInfo.Width : displayInfo.Height,
                NativeDeviceDetails = nativeDeviceDetails,
                WebDeviceDetails = null,
                PlaybackCapabilities = new CreateDeviceRequestPlaybackCapibilities()
                {
                    SupportedMediaFormatIds = supportedMediaFormats.Select(x => x.Id).ToList(),
                    SupportedSubtitlesCodecs = ["webvtt"], // TODO - For now we limit to webvtt
                    SupportsHDR = await GetHdrSupportAsync()
                }
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

        public async Task<List<MediaFormatDto>> GetSupportedMediaFormatsAsync()
        {
            var allFormats = await k7ServerService.GetMediaFormatsAsync();

            var supportedContainers = await codecHelper.GetSupportedContainersAsync();
            var supportedAudioCodecs = await codecHelper.GetSupportedAudioCodecsAsync();
            var supportedVideoCodecs = await codecHelper.GetSupportedVideoCodecsAsync();

            var containerSet = new HashSet<string>(supportedContainers ?? [], StringComparer.OrdinalIgnoreCase);
            var audioSet = new HashSet<string>(supportedAudioCodecs ?? [], StringComparer.OrdinalIgnoreCase);
            var videoSet = new HashSet<string>(supportedVideoCodecs ?? [], StringComparer.OrdinalIgnoreCase);

            var supported = allFormats.Where(f => f switch
            {
                AudioMediaFormatDto audio =>
                    containerSet.Contains(audio.Container) &&
                    audioSet.Contains(audio.Codec),

                VideoMediaFormatDto video =>
                    containerSet.Contains(video.Container) &&
                    videoSet.Contains(video.VideoCodec) &&
                    (string.IsNullOrEmpty(video.AudioCodec) || audioSet.Contains(video.AudioCodec)),

                _ => false
            }).ToList();

            return supported;
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
