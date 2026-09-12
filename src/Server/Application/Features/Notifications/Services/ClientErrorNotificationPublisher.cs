using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Notifications.Services;

/// <summary>
/// Publishes <see cref="ClientErrorReportedEvent"/> with a per-device/message debounce so a flapping client
/// does not flood outbound webhooks. Logging of every report stays in the HTTP endpoint.
/// </summary>
public sealed class ClientErrorNotificationPublisher(
    IDomainEventPublisher publisher,
    ILogger<ClientErrorNotificationPublisher> logger)
{
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMinutes(2);
    private readonly ConcurrentDictionary<string, long> _lastPublishTicks = new(StringComparer.Ordinal);

    public async Task TryPublishAsync(
        string message,
        string? source,
        string? stackTrace,
        string? deviceId,
        string? deviceName,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(deviceId, message);
        var now = DateTime.UtcNow.Ticks;
        if (_lastPublishTicks.TryGetValue(key, out var last)
            && now - last < DebounceWindow.Ticks)
        {
            logger.LogDebug(
                "Skipping client-error notification (debounced) for {UserName} device {DeviceId}",
                userName,
                deviceId);
            return;
        }

        _lastPublishTicks[key] = now;
        Prune(now);

        var truncatedStack = Truncate(stackTrace, 1500);
        await publisher.PublishAsync(
            new ClientErrorReportedEvent(
                Truncate(message, 1000) ?? "",
                Truncate(source, 200),
                truncatedStack,
                deviceId,
                deviceName,
                userName),
            cancellationToken);
    }

    private void Prune(long now)
    {
        if (_lastPublishTicks.Count < 500)
            return;

        foreach (var pair in _lastPublishTicks)
        {
            if (now - pair.Value > DebounceWindow.Ticks * 4)
                _lastPublishTicks.TryRemove(pair.Key, out _);
        }
    }

    private static string BuildKey(string? deviceId, string message)
    {
        var normalized = (message ?? "").Trim();
        if (normalized.Length > 200)
            normalized = normalized[..200];

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];
        return $"{deviceId ?? "unknown"}|{hash}";
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
            return value;

        return value[..max] + "...";
    }
}
