using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Applies <see cref="VideoPlayerSettingsDto"/> updates received via SignalR to the local player.
/// Singleton: resolves scoped player/JS services per notification via <see cref="IServiceScopeFactory"/>.
/// </summary>
public sealed class VideoPlayerUxSettingsSync : IVideoPlayerUxSettingsSync
{
    private readonly IVideoPlayerSettingsHubEvents _hub;
    private readonly IServiceScopeFactory _scopeFactory;

    public VideoPlayerUxSettingsSync(
        IVideoPlayerSettingsHubEvents hubEvents,
        IServiceScopeFactory scopeFactory)
    {
        _hub = hubEvents;
        _scopeFactory = scopeFactory;
        _hub.VideoPlayerSettingsUpdated += OnVideoPlayerSettingsUpdated;
    }

    public void Dispose() =>
        _hub.VideoPlayerSettingsUpdated -= OnVideoPlayerSettingsUpdated;

    private void OnVideoPlayerSettingsUpdated(VideoPlayerSettingsDto settings) =>
        ApplyAsync(settings).FireAndForget();

    private async Task ApplyAsync(VideoPlayerSettingsDto settings)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var player = scope.ServiceProvider.GetRequiredService<IPlayerService>();
        var device = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        var js = scope.ServiceProvider.GetRequiredService<IJSRuntime>();

        player.ApplyVideoPlayerUxSettings(settings);
        var deviceType = device.CachedDeviceType ?? await device.GetDeviceTypeAsync();
        await SubtitleStyleApplicator.ApplyAsync(js, settings, deviceType);
    }
}
