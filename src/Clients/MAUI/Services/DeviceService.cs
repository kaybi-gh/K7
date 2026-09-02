using K7.Clients.MAUI.Interfaces;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos.Devices;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;
using DeviceType = K7.Server.Domain.Enums.DeviceType;

namespace K7.Clients.MAUI.Services;

public class DeviceService(ICodecService codecHelper, IDeviceIdService deviceIdService, IDeviceStorageService deviceStorageService, IMediaService mediaService) : IDeviceService
{
    private readonly DeviceType _cachedDeviceType = ResolveDeviceType();
    private readonly object _requestCacheLock = new();
    private Task<CreateDeviceRequest>? _cachedCreateRequest;
    private Task<List<MediaFormatDto>>? _cachedSupportedFormats;

    public DeviceType? CachedDeviceType => _cachedDeviceType;

    public Task<CreateDeviceRequest> GenerateCreateDeviceRequestAsync()
    {
        lock (_requestCacheLock)
        {
            return _cachedCreateRequest ??= GenerateCreateDeviceRequestCoreAsync();
        }
    }

    private async Task<CreateDeviceRequest> GenerateCreateDeviceRequestCoreAsync()
    {
        var supportedMediaFormats = await GetSupportedMediaFormatsAsync();
        var nativeDeviceDetails = await GetNativeDeviceDetailsAsync();
        var displayInfo = await MainThread.InvokeOnMainThreadAsync(() => DeviceDisplay.MainDisplayInfo);
        var landscape = displayInfo.Orientation == DisplayOrientation.Landscape;
        var (displayWidth, displayHeight) = DisplayPixelSize.FromDip(
            displayInfo.Width,
            displayInfo.Height,
            displayInfo.Density,
            landscape);

        var operatingSystem = await GetOperatingSystemAsync();
        return new CreateDeviceRequest
        {
            DeviceUniqueId = deviceIdService.GetDeviceId(),
            DeviceName = BuildDeviceName(_cachedDeviceType, operatingSystem),
            ClientType = GetClientType(),
            DeviceType = _cachedDeviceType,
            OperatingSystem = operatingSystem,
            OperatingSystemVersion = nativeDeviceDetails.RawVersion,
            DisplayHeight = displayHeight,
            DisplayWidth = displayWidth,
            NativeDeviceDetails = nativeDeviceDetails,
            WebDeviceDetails = null,
            PlaybackCapabilities = new CreateDeviceRequestPlaybackCapibilities()
            {
                SupportedMediaFormatIds = await BuildSupportedMediaFormatIdsAsync(supportedMediaFormats),
                SupportedSubtitlesCodecs = ["webvtt"],
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

    public Task<DeviceType> GetDeviceTypeAsync() => Task.FromResult(_cachedDeviceType);

    public Task<OperatingSystem> GetOperatingSystemAsync()
    {
        return Task.FromResult(MapOperatingSystem(DeviceInfo.Platform));
    }

    public async Task<DeviceCodecSummaryDto> GetDeviceCodecSummaryAsync()
    {
        var containers = await codecHelper.GetSupportedContainersAsync();
        var audioCodecs = await codecHelper.GetSupportedAudioCodecsAsync();
        var videoCodecs = await codecHelper.GetSupportedVideoCodecsAsync();

        return new DeviceCodecSummaryDto
        {
            Containers = containers ?? [],
            AudioCodecs = audioCodecs ?? [],
            VideoCodecs = videoCodecs ?? []
        };
    }

    public Task<List<MediaFormatDto>> GetSupportedMediaFormatsAsync()
    {
        lock (_requestCacheLock)
        {
            return _cachedSupportedFormats ??= GetSupportedMediaFormatsCoreAsync();
        }
    }

    private async Task<List<MediaFormatDto>> GetSupportedMediaFormatsCoreAsync()
    {
        var allFormats = await mediaService.GetMediaFormatsAsync();

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

        var hevc = supported.Count(f => f is VideoMediaFormatDto video
            && video.VideoCodec.Equals("hevc", StringComparison.OrdinalIgnoreCase));
        var matroska = supported.Count(f => f.Container.Equals("matroska", StringComparison.OrdinalIgnoreCase));
        System.Diagnostics.Debug.WriteLine(
            "K7 device formats n="
            + supported.Count
            + " hevc="
            + hevc
            + " matroska="
            + matroska);

        return supported;
    }

    public void InvalidatePlaybackCapabilityCache()
    {
        lock (_requestCacheLock)
        {
            _cachedCreateRequest = null;
            _cachedSupportedFormats = null;
        }
    }

    private async Task<List<string>> BuildSupportedMediaFormatIdsAsync(List<MediaFormatDto> supportedMediaFormats)
    {
        var ids = supportedMediaFormats.Select(x => x.Id).ToList();
        try
        {
            var profiles = await codecHelper.GetSupportedVideoProfilesAsync();
            if (profiles is { Length: > 0 })
                ids.AddRange(profiles);
        }
        catch
        {
        }

        return ids;
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

    public string? GetLocalFileUrl(string? localPath)
    {
        if (string.IsNullOrEmpty(localPath))
            return null;

        var downloadsBase = Path.Combine(FileSystem.AppDataDirectory, "downloads");
        if (!localPath.StartsWith(downloadsBase, StringComparison.OrdinalIgnoreCase))
            return null;

        var relativePath = localPath[downloadsBase.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedPath = relativePath.Replace('\\', '/');
        return $"https://k7-local-files/{normalizedPath}";
    }

    private static DeviceType ResolveDeviceType()
    {
        var mapped = MapDeviceType(DeviceInfo.Idiom);
        if (mapped == DeviceType.TV)
            return DeviceType.TV;

#if ANDROID
        // Many Android TV boxes report Tablet/Phone idiom; UiMode is authoritative.
        if (IsAndroidTelevision())
            return DeviceType.TV;
#endif

        return mapped;
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

#if ANDROID
    private static bool IsAndroidTelevision()
    {
        var context = global::Android.App.Application.Context;
        var uiMode = context.Resources?.Configuration?.UiMode ?? 0;
        return (uiMode & global::Android.Content.Res.UiMode.TypeMask)
            == global::Android.Content.Res.UiMode.TypeTelevision;
    }
#endif

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

    private static string BuildDeviceName(DeviceType deviceType, OperatingSystem operatingSystem)
    {
        var platform = deviceType == DeviceType.Unknown ? "Device" : deviceType.ToString();
        var client = operatingSystem switch
        {
            OperatingSystem.Windows => "Windows",
            OperatingSystem.Android => "Android",
            OperatingSystem.iOS => "iOS",
            OperatingSystem.MacCatalyst => "macOS",
            _ => "App"
        };
        return $"{client} ({platform})";
    }
}
