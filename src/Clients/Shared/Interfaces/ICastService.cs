namespace K7.Clients.Shared.Interfaces;

public interface ICastService
{
    bool IsAvailable { get; }
    bool IsCasting { get; }
    IReadOnlyList<CastDeviceInfo> DiscoveredDevices { get; }

    event Action? StateChanged;
    event Action<IReadOnlyList<CastDeviceInfo>>? DevicesDiscovered;
    event Action<CastMediaStatus>? MediaStatusUpdated;

    Task StartDiscoveryAsync();
    Task StopDiscoveryAsync();
    Task CastAsync(CastMediaRequest request);
    Task StopCastingAsync();
    Task SendTransportCommandAsync(CastTransportCommand command);
}

public sealed record CastDeviceInfo(string Id, string Name);

public sealed record CastMediaRequest
{
    public required string StreamUrl { get; init; }
    public required string ContentType { get; init; }
    public string? Title { get; init; }
    public string? Subtitle { get; init; }
    public string? ThumbnailUrl { get; init; }
    public double? Duration { get; init; }
    public double StartPosition { get; init; }
}

public enum CastTransportCommand
{
    Play,
    Pause,
    Stop
}

public sealed record CastMediaStatus(string State, double Position, double Duration, double Volume);
