using K7.Clients.Shared.Domain.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos.Devices;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;
using Microsoft.JSInterop;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Clients.Web.Services;

public class DeviceService(IJSRuntime jsRuntime, IK7ServerService k7ServerService, IDeviceStorageService deviceStorageService) : IDeviceService
{
    public async Task<CreateDeviceRequest> GenerateCreateDeviceRequestAsync()
    {
        var parsedUserAgent = await jsRuntime.InvokeAsync<ParsedUserAgent>("getParsedUserAgent");
        var displayHeight = await jsRuntime.InvokeAsync<int>("getDisplayHeight");
        var displayWidth = await jsRuntime.InvokeAsync<int>("getDisplayWidth");
        var supportedMediaFormats = await GetSupportedMediaFormatsAsync();
        var webDeviceDetails = await GetWebDeviceDetailsAsync();
        var deviceType = MapDeviceType(parsedUserAgent.PlatformType);
        var browser = MapBrowser(parsedUserAgent.BrowserName);
        var operatingSystem = MapOperatingSystem(parsedUserAgent.OsName);
        
        var deviceName = BuildDeviceName(browser.ToString(), deviceType);

        return new CreateDeviceRequest
        {
            DeviceUniqueId = GetDeviceUniqueId(),
            DeviceName = deviceName,
            ClientType = GetClientType(),
            DeviceType = deviceType,
            OperatingSystem = operatingSystem,
            OperatingSystemVersion = webDeviceDetails.RawOperatingSystemVersion,
            DisplayHeight = displayHeight,
            DisplayWidth = displayWidth,
            NativeDeviceDetails = null,
            WebDeviceDetails = webDeviceDetails,
            PlaybackCapabilities = new CreateDeviceRequestPlaybackCapibilities()
            {
                SupportedMediaFormatIds = supportedMediaFormats.Select(x => x.Id),
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
        return null; // Not possible in web client
    }

    public ClientType GetClientType()
    {
        return ClientType.Web;
    }

    public async Task<DeviceType> GetDeviceTypeAsync()
    {
        var parsedUserAgent = await jsRuntime.InvokeAsync<ParsedUserAgent>("getParsedUserAgent");
        return MapDeviceType(parsedUserAgent.PlatformType);
    }

    public async Task<OperatingSystem> GetOperatingSystemAsync()
    {
        var parsedUserAgent = await jsRuntime.InvokeAsync<ParsedUserAgent>("getParsedUserAgent");
        return MapOperatingSystem(parsedUserAgent.OsName);
    }

    public async Task<List<MediaFormatDto>> GetSupportedMediaFormatsAsync()
    {
        var allFormats = await k7ServerService.GetMediaFormatsAsync();

        var supportedContainers = await jsRuntime.InvokeAsync<string[]>("getSupportedContainersAsync");
        var supportedAudioCodecs = await jsRuntime.InvokeAsync<string[]>("getSupportedAudioCodecsAsync");
        var supportedVideoCodecs = await jsRuntime.InvokeAsync<string[]>("getSupportedVideoCodecsAsync");

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

    public async Task<bool> GetHdrSupportAsync()
    {
        return await jsRuntime.InvokeAsync<bool>("getHdrSupport");
    }

    private static Browser MapBrowser(string? browserName)
    {
        return browserName?.ToLowerInvariant() switch
        {
            "chrome" => Browser.Chrome,
            "edge" => Browser.Edge,
            "firefox" => Browser.Firefox,
            "opera" => Browser.Opera,
            "safari" => Browser.Safari,
            _ => Browser.Unknown
        };
    }

    private static DeviceType MapDeviceType(string? platformType)
    {
        return platformType?.ToLowerInvariant() switch
        {
            "mobile" => DeviceType.Phone,
            "tablet" => DeviceType.Tablet,
            "desktop" => DeviceType.Desktop,
            "tv" => DeviceType.TV,
            _ => DeviceType.Unknown
        };
    }

    private static OperatingSystem MapOperatingSystem(string? osName)
    {
        return osName?.ToLowerInvariant() switch
        {
            "windows" => OperatingSystem.Windows,
            "android" => OperatingSystem.Android,
            "ios" => OperatingSystem.iOS,
            "macos" => OperatingSystem.MacCatalyst,
            _ => OperatingSystem.Unknown
        };
    }

    private static string BuildDeviceName(string? browserName, DeviceType deviceType)
    {
        var readableBrowser = string.IsNullOrWhiteSpace(browserName) ? "Unknown browser" : browserName;
        var readableDeviceType = deviceType.ToString();
        return $"{readableBrowser} ({readableDeviceType})";
    }

    public async Task<WebDeviceDetailsDto> GetWebDeviceDetailsAsync()
    {
        var parsedUserAgent = await jsRuntime.InvokeAsync<ParsedUserAgent>("getParsedUserAgent");
        return new WebDeviceDetailsDto
        {
            Browser = MapBrowser(parsedUserAgent.BrowserName),
            RawUserAgent = await jsRuntime.InvokeAsync<string>("getRawUserAgent"),
            RawBrowserName = parsedUserAgent.BrowserName,
            RawBrowserVersion = parsedUserAgent.BrowserVersion,
            RawOperatingSystemName = parsedUserAgent.OsName,
            RawOperatingSystemVersion = parsedUserAgent.OsVersion,
            RawOperatingSystemVersionName = parsedUserAgent.OsVersionName,
            RawPlatformType = parsedUserAgent.PlatformType
        };
    }

    public Task<NativeDeviceDetailsDto> GetNativeDeviceDetailsAsync()
    {
        throw new InvalidOperationException($"Cannot fetch {nameof(NativeDeviceDetailsDto)} from web device.");
    }

    internal sealed record ParsedUserAgent
    {
        public string? BrowserName { get; init; }
        public string? BrowserVersion { get; init; }
        public string? OsName { get; init; }
        public string? OsVersion { get; init; }
        public string? OsVersionName { get; init; }
        public string? PlatformType { get; init; }
        public string? EngineName { get; init; }
        public string? EngineVersion { get; init; }
    }
}
