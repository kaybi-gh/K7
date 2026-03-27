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
    Task<DeviceType> GetDeviceTypeAsync();
    Task<OperatingSystem> GetOperatingSystemAsync();
    Task<NativeDeviceDetailsDto> GetNativeDeviceDetailsAsync();
    Task<WebDeviceDetailsDto> GetWebDeviceDetailsAsync();
    Task<List<MediaFormatDto>> GetSupportedMediaFormatsAsync();
    Task<bool> GetHdrSupportAsync();
    Task<CreateDeviceRequest> GenerateCreateDeviceRequestAsync();
    
}
