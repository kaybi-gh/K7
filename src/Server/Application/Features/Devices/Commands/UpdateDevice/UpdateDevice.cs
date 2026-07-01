using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Devices.Commands.UpdateDevice;

public record UpdateDeviceCommand : IRequest
{
    public Guid Id { get; init; }
    public required UpdateDeviceRequest UpdateDeviceRequest { get; init; }
}

public class UpdateDeviceCommandHandler : IRequestHandler<UpdateDeviceCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateDeviceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateDeviceCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Devices
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        var update = request.UpdateDeviceRequest;
        entity.DeviceName = update.DeviceName;
        entity.ClientType = update.ClientType;
        entity.DeviceType = update.DeviceType;
        entity.OperatingSystem = update.OperatingSystem;
        entity.OperatingSystemVersion = update.OperatingSystemVersion;
        entity.DisplayHeight = update.DisplayHeight;
        entity.DisplayWidth = update.DisplayWidth;
        entity.PlaybackCapabilities = new DevicePlaybackCapabilities
        {
            SupportedMediaFormatIds = update.PlaybackCapabilities.SupportedMediaFormatIds?.ToList() ?? [],
            SupportedSubtitlesCodecs = update.PlaybackCapabilities.SupportedSubtitlesCodecs?.ToList() ?? [],
            SupportsHDR = update.PlaybackCapabilities.SupportsHDR
        };
        entity.LastSeen = DateTimeOffset.UtcNow;

        if (update.ClientType == ClientType.Native && update.NativeDeviceDetails is not null)
        {
            entity.NativeDeviceDetails = new NativeDeviceDetails
            {
                RawDeviceType = update.NativeDeviceDetails.RawDeviceType,
                RawIdiom = update.NativeDeviceDetails.RawIdiom,
                RawManufacturer = update.NativeDeviceDetails.RawManufacturer,
                RawModel = update.NativeDeviceDetails.RawModel,
                RawName = update.NativeDeviceDetails.RawName,
                RawPlatform = update.NativeDeviceDetails.RawPlatform,
                RawVersion = update.NativeDeviceDetails.RawVersion
            };
            entity.WebDeviceDetails = null;
        }
        else if (update.ClientType == ClientType.Web && update.WebDeviceDetails is not null)
        {
            entity.WebDeviceDetails = new WebDeviceDetails
            {
                Browser = update.WebDeviceDetails.Browser,
                RawUserAgent = update.WebDeviceDetails.RawUserAgent,
                RawBrowserName = update.WebDeviceDetails.RawBrowserName,
                RawBrowserVersion = update.WebDeviceDetails.RawBrowserVersion,
                RawOperatingSystemName = update.WebDeviceDetails.RawOperatingSystemName,
                RawOperatingSystemVersion = update.WebDeviceDetails.RawOperatingSystemVersion,
                RawOperatingSystemVersionName = update.WebDeviceDetails.RawOperatingSystemVersionName,
                RawPlatformType = update.WebDeviceDetails.RawPlatformType,
                RawEngineName = update.WebDeviceDetails.RawEngineName,
                RawEngineVersion = update.WebDeviceDetails.RawEngineVersion
            };
            entity.NativeDeviceDetails = null;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
