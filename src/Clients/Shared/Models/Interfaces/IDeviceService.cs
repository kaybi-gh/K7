using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Clients.Shared.Domain.Interfaces;

public interface IDeviceService
{
    Task<CreateDeviceRequest> GenerateCreateDeviceRequestAsync();
    string? GetDeviceId();
    Task<DeviceType> GetDeviceTypeAsync();
    OperatingSystem GetOperatingSystem();
    Task<List<MediaFormatDto>> GetSupportedMediaFormatsAsync();
    Task<bool> GetHdrSupportAsync();

    string GetPlatform();
}
