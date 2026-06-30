using System.Security.Cryptography;
using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Responses;
using Microsoft.EntityFrameworkCore;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Server.Application.Features.Devices.Commands.BulkResolveImportDevices;

[Authorize(Roles = Roles.Administrator)]
public record BulkResolveImportDevicesCommand : IRequest<BulkResolveImportDevicesResponse>
{
    public required IReadOnlyList<BulkResolveImportDevicesRequest.ImportDeviceDescriptor> Items { get; init; }
}

public class BulkResolveImportDevicesCommandHandler(IApplicationDbContext context)
    : IRequestHandler<BulkResolveImportDevicesCommand, BulkResolveImportDevicesResponse>
{
    private const int SaveBatchSize = 200;

    public async Task<BulkResolveImportDevicesResponse> Handle(
        BulkResolveImportDevicesCommand request,
        CancellationToken cancellationToken)
    {
        var eligible = request.Items
            .Where(d => !string.IsNullOrWhiteSpace(d.DeviceName)
                || !string.IsNullOrWhiteSpace(d.Platform)
                || !string.IsNullOrWhiteSpace(d.Player))
            .ToList();

        var results = new Dictionary<string, BulkResolveImportDevicesResponse.DeviceMatchResult>(StringComparer.Ordinal);
        var toCreate = new List<(BulkResolveImportDevicesRequest.ImportDeviceDescriptor Descriptor, string UniqueId)>();

        foreach (var descriptor in request.Items)
        {
            if (string.IsNullOrWhiteSpace(descriptor.DeviceName)
                && string.IsNullOrWhiteSpace(descriptor.Platform)
                && string.IsNullOrWhiteSpace(descriptor.Player))
            {
                results[descriptor.Key] = new BulkResolveImportDevicesResponse.DeviceMatchResult
                {
                    Key = descriptor.Key,
                    DeviceId = null,
                    WasCreated = false
                };
            }
        }

        foreach (var descriptor in eligible)
        {
            toCreate.Add((descriptor, BuildImportDeviceUniqueId(descriptor)));
        }

        if (toCreate.Count > 0)
        {
            var uniqueIds = toCreate.Select(x => x.UniqueId).Distinct().ToList();
            var existingDevices = await context.Devices
                .AsNoTracking()
                .Where(d => d.DeviceUniqueId != null && uniqueIds.Contains(d.DeviceUniqueId))
                .ToDictionaryAsync(d => d.DeviceUniqueId!, cancellationToken);

            var pending = new List<(BulkResolveImportDevicesRequest.ImportDeviceDescriptor Descriptor, Device Entity)>();

            foreach (var (descriptor, uniqueId) in toCreate)
            {
                if (existingDevices.TryGetValue(uniqueId, out var existing))
                {
                    results[descriptor.Key] = new BulkResolveImportDevicesResponse.DeviceMatchResult
                    {
                        Key = descriptor.Key,
                        DeviceId = existing.Id,
                        WasCreated = false
                    };
                    continue;
                }

                if (results.ContainsKey(descriptor.Key))
                    continue;

                var device = CreateDevice(descriptor, uniqueId);
                context.Devices.Add(device);
                existingDevices[uniqueId] = device;
                pending.Add((descriptor, device));

                if (pending.Count >= SaveBatchSize)
                {
                    await context.SaveChangesAsync(cancellationToken);
                    FlushPending(pending, results);
                }
            }

            if (pending.Count > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
                FlushPending(pending, results);
            }
        }

        return new BulkResolveImportDevicesResponse
        {
            Results = request.Items
                .Select(i => results.GetValueOrDefault(i.Key) ?? new BulkResolveImportDevicesResponse.DeviceMatchResult
                {
                    Key = i.Key,
                    DeviceId = null,
                    WasCreated = false
                })
                .ToList()
        };
    }

    private static void FlushPending(
        List<(BulkResolveImportDevicesRequest.ImportDeviceDescriptor Descriptor, Device Entity)> pending,
        Dictionary<string, BulkResolveImportDevicesResponse.DeviceMatchResult> results)
    {
        foreach (var (descriptor, entity) in pending)
        {
            results[descriptor.Key] = new BulkResolveImportDevicesResponse.DeviceMatchResult
            {
                Key = descriptor.Key,
                DeviceId = entity.Id,
                WasCreated = true
            };
        }

        pending.Clear();
    }

    private static Device CreateDevice(BulkResolveImportDevicesRequest.ImportDeviceDescriptor descriptor, string uniqueId)
    {
        var platform = descriptor.Platform ?? string.Empty;
        var player = descriptor.Player ?? string.Empty;
        var deviceName = ResolveDisplayName(descriptor);
        var clientType = MapClientType(platform, player);
        var operatingSystem = MapOperatingSystem(platform, player);
        var deviceType = MapDeviceType(platform, player, deviceName);

        var device = new Device
        {
            DeviceUniqueId = uniqueId,
            DeviceName = deviceName,
            ClientType = clientType,
            DeviceType = deviceType,
            OperatingSystem = operatingSystem,
            LastSeen = DateTimeOffset.UtcNow
        };

        device.AddDomainEvent(new DeviceCreatedEvent(device));

        if (clientType is ClientType.Web)
        {
            device.WebDeviceDetails = new WebDeviceDetails
            {
                RawBrowserName = ExtractBrowserName(player),
                RawOperatingSystemName = platform,
                RawPlatformType = platform
            };
        }
        else if (!string.IsNullOrWhiteSpace(platform) || !string.IsNullOrWhiteSpace(player))
        {
            device.NativeDeviceDetails = new NativeDeviceDetails
            {
                RawName = deviceName,
                RawPlatform = platform,
                RawModel = player
            };
        }

        return device;
    }

    private static string ResolveDisplayName(BulkResolveImportDevicesRequest.ImportDeviceDescriptor descriptor)
    {
        if (!string.IsNullOrWhiteSpace(descriptor.DeviceName))
            return descriptor.DeviceName.Trim();

        if (!string.IsNullOrWhiteSpace(descriptor.Player))
            return descriptor.Player.Trim();

        if (!string.IsNullOrWhiteSpace(descriptor.Platform))
            return descriptor.Platform.Trim();

        return "Imported device";
    }

    private static string BuildImportDeviceUniqueId(BulkResolveImportDevicesRequest.ImportDeviceDescriptor descriptor)
    {
        var payload = string.Join('\n',
            (descriptor.DeviceName ?? string.Empty).Trim().ToLowerInvariant(),
            (descriptor.Platform ?? string.Empty).Trim().ToLowerInvariant(),
            (descriptor.Player ?? string.Empty).Trim().ToLowerInvariant());

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        return $"import:{hash[..32].ToLowerInvariant()}";
    }

    private static ClientType MapClientType(string platform, string player)
    {
        var combined = $"{platform} {player}".ToLowerInvariant();
        if (combined.Contains("chrome")
            || combined.Contains("firefox")
            || combined.Contains("safari")
            || combined.Contains("edge")
            || combined.Contains("browser")
            || combined.Contains("web"))
        {
            return ClientType.Web;
        }

        return ClientType.Native;
    }

    private static OperatingSystem MapOperatingSystem(string platform, string player)
    {
        var combined = $"{platform} {player}".ToLowerInvariant();

        if (combined.Contains("android"))
            return OperatingSystem.Android;

        if (combined.Contains("ios")
            || combined.Contains("iphone")
            || combined.Contains("ipad")
            || combined.Contains("ipod")
            || combined.Contains("tvos"))
        {
            return OperatingSystem.iOS;
        }

        if (combined.Contains("windows"))
            return OperatingSystem.Windows;

        if (combined.Contains("linux")
            || combined.Contains("ubuntu")
            || combined.Contains("debian"))
        {
            return OperatingSystem.Linux;
        }

        if (combined.Contains("mac") || combined.Contains("osx") || combined.Contains("os x"))
            return OperatingSystem.MacCatalyst;

        return OperatingSystem.Unknown;
    }

    private static DeviceType MapDeviceType(string platform, string player, string deviceName)
    {
        var combined = $"{platform} {player} {deviceName}".ToLowerInvariant();

        if (combined.Contains("tv")
            || combined.Contains("shield")
            || combined.Contains("roku")
            || combined.Contains("fire tv")
            || combined.Contains("firetv")
            || combined.Contains("appletv")
            || combined.Contains("android tv")
            || combined.Contains("chromecast"))
        {
            return DeviceType.TV;
        }

        if (combined.Contains("ipad") || combined.Contains("tablet"))
            return DeviceType.Tablet;

        if (combined.Contains("iphone")
            || combined.Contains("phone")
            || combined.Contains("mobile")
            || combined.Contains("pixel"))
        {
            return DeviceType.Phone;
        }

        if (combined.Contains("watch"))
            return DeviceType.Watch;

        if (combined.Contains("desktop") || combined.Contains("pc") || combined.Contains("laptop"))
            return DeviceType.Desktop;

        return DeviceType.Unknown;
    }

    private static string? ExtractBrowserName(string player)
    {
        var lower = player.ToLowerInvariant();

        if (lower.Contains("chrome")) return "Chrome";
        if (lower.Contains("firefox")) return "Firefox";
        if (lower.Contains("safari")) return "Safari";
        if (lower.Contains("edge")) return "Edge";

        return string.IsNullOrWhiteSpace(player) ? null : player;
    }
}
