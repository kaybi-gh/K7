using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.Shared.Services;

public sealed class RemotePlaybackLauncher(
    K7HubClient hubClient,
    IRemoteControlService remoteControl,
    IDeviceStorageService deviceStorage,
    IServiceProvider services)
{
    public async Task PlayOnDeviceAsync(ConnectedDeviceDto device, RemotePlaybackRequestDto request)
    {
        var handover = services.GetService<RemotePlaybackHandler>();
        handover?.DiscardPendingHandover();

        var senderDeviceId = deviceStorage.Get(PreferenceKeys.DEVICE_ID);
        if (Guid.TryParse(senderDeviceId, out var senderId))
            request = request with { SenderDeviceId = senderId };

        // Hub excludes the sender from PlaybackTakenOver - stop local playback here.
        if (!request.AttachOnly && handover is not null)
            await handover.StopLocalPlaybackQuietlyAsync();

        await hubClient.RequestRemotePlaybackAsync(device.DeviceId, request);
        remoteControl.StartSession(device.DeviceId, ConnectedDeviceLabels.GetDisplayName(device), request);
    }

    public async Task AttachToDeviceAsync(NowPlayingSessionDto session, string? coverUrl = null)
    {
        if (session.DeviceId is not Guid deviceId)
            return;

        var indexedFileId = session.IndexedFileId;
        if (indexedFileId is null && session.MediaId is Guid mediaId)
        {
            var mediaService = services.GetService<IMediaService>();
            if (mediaService is not null)
            {
                try
                {
                    var media = await mediaService.GetMediaAsync(mediaId);
                    indexedFileId = media?.IndexedFiles?.FirstOrDefault()?.Id;
                }
                catch
                {
                    // Attach still proceeds only when we have a file id.
                }
            }
        }

        if (indexedFileId is not Guid fileId || fileId == Guid.Empty)
            return;

        var device = new ConnectedDeviceDto
        {
            DeviceId = deviceId,
            DeviceName = session.DeviceName ?? session.DeviceType ?? "Device",
            DeviceType = session.DeviceType ?? "Unknown"
        };

        var request = new RemotePlaybackRequestDto
        {
            IndexedFileId = fileId,
            StartPosition = session.Position > 0 ? session.Position : null,
            IsAudio = session.IsAudio,
            MediaId = session.MediaId,
            Title = session.MediaTitle,
            CoverUrl = coverUrl ?? session.ThumbnailUrl,
            Duration = session.Duration > 0 ? session.Duration : null,
            AttachOnly = true
        };

        await PlayOnDeviceAsync(device, request);
    }

    public Task NotifyLocalTakeoverAsync(
        string? title,
        Guid? mediaId,
        Guid? indexedFileId,
        bool isAudio,
        string? coverUrl,
        double position,
        double duration)
    {
        services.GetService<RemotePlaybackHandler>()?.DiscardPendingHandover();

        var deviceIdRaw = deviceStorage.Get(PreferenceKeys.DEVICE_ID);
        var deviceName = deviceStorage.Get(PreferenceKeys.DEVICE_NAME);
        _ = Guid.TryParse(deviceIdRaw, out var deviceId);

        return hubClient.NotifyPlaybackTakenOverAsync(new PlaybackTakenOverDto
        {
            NewDeviceId = deviceId,
            NewDeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Device" : deviceName.Trim(),
            Title = title,
            MediaId = mediaId,
            IndexedFileId = indexedFileId,
            IsAudio = isAudio,
            CoverUrl = coverUrl,
            Position = position,
            Duration = duration
        });
    }

    public async Task StopOnDeviceAsync(Guid deviceId)
    {
        await hubClient.SendRemoteTransportCommandAsync(deviceId, new RemoteTransportCommandDto
        {
            Action = RemoteTransportAction.Stop
        });
    }
}
