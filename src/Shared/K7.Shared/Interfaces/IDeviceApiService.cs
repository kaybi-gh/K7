using K7.Shared.Dtos;
using K7.Shared.Dtos.Devices;
using K7.Shared.Dtos.Requests;

namespace K7.Shared.Interfaces;

public interface IDeviceApiService
{
    Task<Guid> CreateDeviceAsync(CreateDeviceRequest request, CancellationToken cancellationToken = default);
    Task AttachCurrentUserToDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<PaginatedListDto<DeviceDto>?> GetDevicesAsync(GetDevicesQuery? query = null, CancellationToken cancellationToken = default);
    Task DeleteDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);
}
