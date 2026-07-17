using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Server.Application.Features.Devices.Queries.GetDevices;

public sealed record GetDevicesQuery : IRequest<PaginatedList<Device>>
{
    public Guid[]? Ids { get; init; }
    public Guid[]? UserIds { get; init; }
    public EnumHashSetQueryParam<ClientType>? ClientTypes { get; init; }
    public EnumHashSetQueryParam<DeviceType>? DeviceTypes { get; init; }
    public EnumHashSetQueryParam<OperatingSystem>? OperatingSystems { get; init; }
    public EnumHashSetQueryParam<DevicesOrderingOption>? OrderBy { get; init; } = [DevicesOrderingOption.CreatedDesc];
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = PagingDefaults.CompactPageSize;
};

public class GetDevicesQueryHandler : IRequestHandler<GetDevicesQuery, PaginatedList<Device>>
{
    private readonly IApplicationDbContext _context;

    public GetDevicesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<Device>> Handle(GetDevicesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Devices
            .Include(x => x.Users)
            .AsNoTracking()
            .AsQueryable();

        query = ApplyFilters(request, query);
        var orderedQuery = ApplyOrdering(request.OrderBy, query);
        return await orderedQuery.PaginatedListAsync(request.PageNumber, request.PageSize);
    }

    private static IQueryable<Device> ApplyFilters(GetDevicesQuery request, IQueryable<Device> query)
    {
        if (request.Ids?.Length > 0)
        {
            query = query.Where(x => request.Ids.Contains(x.Id));
        }

        if (request.UserIds?.Length > 0)
        {
            query = query.Where(x => request.UserIds.Overlaps(x.Users.Select(x => x.Id).ToArray()));
        }

        if (request.ClientTypes?.Count > 0)
        {
            query = query.Where(x => request.ClientTypes.Contains(x.ClientType));
        }

        if (request.DeviceTypes?.Count > 0)
        {
            query = query.Where(x => request.DeviceTypes.Contains(x.DeviceType));
        }

        if (request.OperatingSystems?.Count > 0)
        {
            query = query.Where(x => request.OperatingSystems.Contains(x.OperatingSystem));
        }

        return query;
    }

    private static IOrderedQueryable<Device> ApplyOrdering(HashSet<DevicesOrderingOption>? orderBy, IQueryable<Device> queryable)
    {
        ArgumentException.ThrowIfNullOrEmpty(nameof(orderBy));
        IOrderedQueryable<Device>? orderedQueryable = null;

        if (orderBy == null || orderBy.Count == 0)
        {
            return queryable.OrderByDescending(x => x.Id);
        }

        foreach (var order in orderBy)
        {
            orderedQueryable = order switch
            {
                DevicesOrderingOption.CreatedAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.Id)
                    : orderedQueryable.ThenBy(x => x.Id),
                DevicesOrderingOption.CreatedDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.Id)
                    : orderedQueryable.ThenByDescending(x => x.Id),
                DevicesOrderingOption.LastSeenAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.LastSeen)
                    : orderedQueryable.ThenBy(x => x.LastSeen),
                DevicesOrderingOption.LastSeenDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.LastSeen)
                    : orderedQueryable.ThenByDescending(x => x.LastSeen),
                _ => throw new InvalidOperationException($"Unsupported media ordering option: {order}")
            };
        }
        return orderedQueryable!;
    }
}
