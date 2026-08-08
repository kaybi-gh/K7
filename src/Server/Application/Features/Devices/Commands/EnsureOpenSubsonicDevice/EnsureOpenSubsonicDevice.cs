using System.Security.Cryptography;
using System.Text;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Devices.Commands.EnsureOpenSubsonicDevice;

[Authorize(Roles = $"{Roles.Guest},{Roles.User},{Roles.Administrator}")]
public record EnsureOpenSubsonicDeviceCommand(string? ClientName) : IRequest<Guid>;

public class EnsureOpenSubsonicDeviceCommandHandler(
    IApplicationDbContext context,
    IUser currentUser) : IRequestHandler<EnsureOpenSubsonicDeviceCommand, Guid>
{
    private const int MaxDeviceNameLength = 200;

    public async Task<Guid> Handle(EnsureOpenSubsonicDeviceCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            throw new ForbiddenAccessException();

        var deviceName = NormalizeDeviceName(request.ClientName);
        var uniqueId = BuildUniqueId(userId, deviceName);

        var device = await context.Devices
            .Include(d => d.Users)
            .FirstOrDefaultAsync(d => d.DeviceUniqueId == uniqueId, cancellationToken);

        if (device is null)
        {
            var domainUser = await context.Users
                .SingleAsync(u => u.Id == userId, cancellationToken);

            device = new Device
            {
                Id = Guid.NewGuid(),
                DeviceUniqueId = uniqueId,
                DeviceName = deviceName,
                ClientType = ClientType.External,
                DeviceType = DeviceType.Unknown,
                LastSeen = DateTimeOffset.UtcNow,
                Users = [domainUser]
            };
            device.AddDomainEvent(new DeviceCreatedEvent(device));
            context.Devices.Add(device);
            await context.SaveChangesAsync(cancellationToken);
            return device.Id;
        }

        if (!string.Equals(device.DeviceName, deviceName, StringComparison.Ordinal))
            device.DeviceName = deviceName;

        if (device.Users.All(u => u.Id != userId))
        {
            var domainUser = await context.Users
                .SingleAsync(u => u.Id == userId, cancellationToken);
            device.Users.Add(domainUser);
        }

        device.LastSeen = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return device.Id;
    }

    public static string NormalizeDeviceName(string? clientName)
    {
        var name = string.IsNullOrWhiteSpace(clientName) ? "OpenSubsonic" : clientName.Trim();
        if (name.Length > MaxDeviceNameLength)
            name = name[..MaxDeviceNameLength];
        return name;
    }

    public static string BuildUniqueId(Guid userId, string deviceName)
    {
        var normalized = deviceName.Trim().ToLowerInvariant();
#pragma warning disable CA5351
        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(normalized)))
            .ToLowerInvariant();
#pragma warning restore CA5351
        return $"opensubsonic:{userId:D}:{hash}";
    }
}
