using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Shared.QueryBuilders;
using Microsoft.AspNetCore.Http;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Server.Application.Features.Devices.Commands.CreateDevice;

public record CreateDeviceCommand : IRequest<IResult>
{
    public string? DeviceUniqueId { get; set; }
    public string? DeviceName { get; set; }
    public DeviceType DeviceType { get; set; } = DeviceType.Unknown;
    public OperatingSystem OperatingSystem { get; set; } = OperatingSystem.Unknown;
    public string? OperatingSystemVersion { get; set; }
    public double DisplayHeight { get; set; }
    public double DisplayWidth { get; set; }
    public ICollection<string> SupportedMediaFormatIds { get; set; } = [];
    public ICollection<string> SupportedSubtitlesCodecs { get; set; } = [];
    public bool SupportsHDR { get; set; }
}

public class CreateDeviceCommandHandler : IRequestHandler<CreateDeviceCommand, IResult>
{
    private readonly IApplicationDbContext _context;

    public CreateDeviceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(CreateDeviceCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.DeviceUniqueId))
        {
            var existingDevice = await _context.Devices.FirstOrDefaultAsync(x => x.DeviceUniqueId == request.DeviceUniqueId, cancellationToken: cancellationToken);
            if (existingDevice != null)
            {
                var existingDeviceQuery = new Shared.Dtos.Requests.GetDeviceQuery()
                {
                    Id = existingDevice.Id
                };
                return Results.Created(GetDeviceQueryUriBuilder.Build(existingDeviceQuery), existingDeviceQuery);
            }
        }
        // TODO - Find existing devices with other properties (browser)

        var entity = new Device
        {
            Id = Guid.NewGuid(),
            DeviceUniqueId = request.DeviceUniqueId,
            DeviceName = request.DeviceName,
            DeviceType = request.DeviceType,
            OperatingSystem = request.OperatingSystem,
            OperatingSystemVersion = request.OperatingSystemVersion,
            DisplayHeight = request.DisplayHeight,
            DisplayWidth = request.DisplayWidth,
            SupportedMediaFormats = [.. Constants.MediaFormats.Where(x => request.SupportedMediaFormatIds.Contains(x.Id))],
            SupportedSubtitlesCodecs = request.SupportedSubtitlesCodecs,
            SupportsHDR = request.SupportsHDR,
            LastSeen = DateTimeOffset.UtcNow
        };

        entity.AddDomainEvent(new DeviceCreatedEvent(entity));
        _context.Devices.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        var query = new Shared.Dtos.Requests.GetDeviceQuery()
        {
            Id = entity.Id
        };

        return Results.Created(GetDeviceQueryUriBuilder.Build(query), query);
    }
}
