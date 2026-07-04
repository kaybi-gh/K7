using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Shared.Dtos.Requests;
using K7.Shared.QueryBuilders;
using Microsoft.AspNetCore.Http;

namespace K7.Server.Application.Features.Devices.Commands.CreateDevice;

public record CreateDeviceCommand : IRequest<IResult>
{
    public required CreateDeviceRequest CreateDeviceRequest { get; set; }
}

public class CreateDeviceCommandHandler : IRequestHandler<CreateDeviceCommand, IResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public CreateDeviceCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<IResult> Handle(CreateDeviceCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.CreateDeviceRequest.DeviceUniqueId))
        {
            var existingDevice = await _context.Devices
                .FirstOrDefaultAsync(x => x.DeviceUniqueId == request.CreateDeviceRequest.DeviceUniqueId, cancellationToken: cancellationToken);
            if (existingDevice != null)
            {
                var existingDeviceQuery = new GetDeviceQuery()
                {
                    Id = existingDevice.Id
                };
                return Results.Created(GetDeviceQueryUriBuilder.Build(existingDeviceQuery), existingDeviceQuery);
            }
        }
        // Duplicate devices without DeviceUniqueId are accepted on web clients.

        var entity = new Device
        {
            Id = Guid.NewGuid(),
            DeviceUniqueId = request.CreateDeviceRequest.DeviceUniqueId,
            DeviceName = request.CreateDeviceRequest.DeviceName,
            ClientType = request.CreateDeviceRequest.ClientType,
            DeviceType = request.CreateDeviceRequest.DeviceType,
            OperatingSystem = request.CreateDeviceRequest.OperatingSystem,
            OperatingSystemVersion = request.CreateDeviceRequest.OperatingSystemVersion,
            DisplayHeight = request.CreateDeviceRequest.DisplayHeight,
            DisplayWidth = request.CreateDeviceRequest.DisplayWidth,
            PlaybackCapabilities = new DevicePlaybackCapabilities
            {
                SupportedMediaFormatIds = request.CreateDeviceRequest.PlaybackCapabilities.SupportedMediaFormatIds?.ToList() ?? [],
                SupportedSubtitlesCodecs = request.CreateDeviceRequest.PlaybackCapabilities.SupportedSubtitlesCodecs?.ToList() ?? [],
                SupportsHDR = request.CreateDeviceRequest.PlaybackCapabilities.SupportsHDR
            },
            LastSeen = DateTimeOffset.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(_user.IdentityId))
        {
            var domainUser = await _context.Users
                .SingleOrDefaultAsync(u => u.IdentityUserId == _user.IdentityId, cancellationToken);

            if (domainUser is not null)
            {
                entity.Users.Add(domainUser);
            }
        }

        if (request.CreateDeviceRequest.ClientType == ClientType.Native && request.CreateDeviceRequest.NativeDeviceDetails != null)
        {
            entity.NativeDeviceDetails = new NativeDeviceDetails
            {
                RawDeviceType = request.CreateDeviceRequest.NativeDeviceDetails.RawDeviceType,
                RawIdiom = request.CreateDeviceRequest.NativeDeviceDetails.RawIdiom,
                RawManufacturer = request.CreateDeviceRequest.NativeDeviceDetails.RawManufacturer,
                RawModel = request.CreateDeviceRequest.NativeDeviceDetails.RawModel,
                RawName = request.CreateDeviceRequest.NativeDeviceDetails.RawName,
                RawPlatform = request.CreateDeviceRequest.NativeDeviceDetails.RawPlatform,
                RawVersion = request.CreateDeviceRequest.NativeDeviceDetails.RawVersion
            };
        }

        if (request.CreateDeviceRequest.ClientType == ClientType.Web && request.CreateDeviceRequest.WebDeviceDetails != null)
        {
            entity.WebDeviceDetails = new WebDeviceDetails
            {
                Browser = request.CreateDeviceRequest.WebDeviceDetails.Browser,
                RawUserAgent = request.CreateDeviceRequest.WebDeviceDetails.RawUserAgent,
                RawBrowserName = request.CreateDeviceRequest.WebDeviceDetails.RawBrowserName,
                RawBrowserVersion = request.CreateDeviceRequest.WebDeviceDetails.RawBrowserVersion,
                RawOperatingSystemName = request.CreateDeviceRequest.WebDeviceDetails.RawOperatingSystemName,
                RawOperatingSystemVersion = request.CreateDeviceRequest.WebDeviceDetails.RawOperatingSystemVersion,
                RawOperatingSystemVersionName = request.CreateDeviceRequest.WebDeviceDetails.RawOperatingSystemVersionName,
                RawPlatformType = request.CreateDeviceRequest.WebDeviceDetails.RawPlatformType,
                RawEngineName = request.CreateDeviceRequest.WebDeviceDetails.RawEngineName,
                RawEngineVersion = request.CreateDeviceRequest.WebDeviceDetails.RawEngineVersion
            };
        }

        entity.AddDomainEvent(new DeviceCreatedEvent(entity));
        _context.Devices.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        var query = new GetDeviceQuery()
        {
            Id = entity.Id
        };

        return Results.Created(GetDeviceQueryUriBuilder.Build(query), query);
    }
}
