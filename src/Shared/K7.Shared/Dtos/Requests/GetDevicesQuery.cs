using K7.Server.Domain.Enums;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Shared.Dtos.Requests;

public sealed record GetDevicesQuery
{
    public Guid[]? Ids { get; init; }
    public Guid[]? UserIds { get; init; }
    public HashSet<ClientType>? ClientTypes { get; init; }
    public HashSet<DeviceType>? DeviceTypes { get; init; }
    public HashSet<OperatingSystem>? OperatingSystems { get; init; }
    public HashSet<DevicesOrderingOption>? OrderBy { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public enum DevicesOrderingOption
{
    CreatedAsc,
    CreatedDesc,
    LastSeenAsc,
    LastSeenDesc
}
