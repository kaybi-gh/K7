using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Devices;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Clients.Shared.Interfaces;

public interface IDeviceService
{
    string? GetDeviceId();
    string? GetDeviceUniqueId();
    ClientType GetClientType();
    /// <summary>
    /// Synchronously known device type when the host can resolve it without JS (e.g. MAUI).
    /// Null until the first successful <see cref="GetDeviceTypeAsync"/> on Web.
    /// </summary>
    DeviceType? CachedDeviceType { get; }
    Task<DeviceType> GetDeviceTypeAsync();
    Task<OperatingSystem> GetOperatingSystemAsync();
    Task<NativeDeviceDetailsDto> GetNativeDeviceDetailsAsync();
    Task<WebDeviceDetailsDto> GetWebDeviceDetailsAsync();
    Task<List<MediaFormatDto>> GetSupportedMediaFormatsAsync();
    Task<DeviceCodecSummaryDto> GetDeviceCodecSummaryAsync();
    Task<bool> GetHdrSupportAsync();
    Task<CreateDeviceRequest> GenerateCreateDeviceRequestAsync();
    string? GetLocalFileUrl(string? localPath);
}
