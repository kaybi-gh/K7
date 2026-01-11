using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos.Devices;

namespace K7.Shared.Dtos;

public sealed record UserDto
{
    public required ICollection<Guid> AccessibleLibraryIds { get; init; }
    public required IEnumerable<DeviceDto> Devices { get; init; }

    public static UserDto FromDomain(User domain)
    {
        return new()
        {
            AccessibleLibraryIds = domain.AccessibleLibraryIds,
            Devices = domain.Devices.Select(DeviceDto.FromDomain)
        };
    }
}
