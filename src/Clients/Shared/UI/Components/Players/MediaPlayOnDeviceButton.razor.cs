using K7.Clients.Shared.Services;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class MediaPlayOnDeviceButton
{
    [Inject] private RemotePlaybackLauncher RemotePlayback { get; set; } = default!;

    [Parameter] public Guid IndexedFileId { get; set; }
    [Parameter] public Guid? MediaId { get; set; }
    [Parameter] public string? Title { get; set; }
    [Parameter] public string? CoverUrl { get; set; }
    [Parameter] public double? Duration { get; set; }
    [Parameter] public double? StartPosition { get; set; }
    [Parameter] public bool IsAudio { get; set; }
    [Parameter] public string? Artist { get; set; }
    [Parameter] public string? AlbumTitle { get; set; }
    [Parameter] public string? ButtonClass { get; set; }

    private Task OnRemoteDeviceSelected(ConnectedDeviceDto device)
    {
        if (IndexedFileId == Guid.Empty)
            return Task.CompletedTask;

        var request = new RemotePlaybackRequestDto
        {
            IndexedFileId = IndexedFileId,
            StartPosition = StartPosition is > 0 ? StartPosition : null,
            IsAudio = IsAudio,
            MediaId = MediaId,
            Title = Title,
            Artist = Artist,
            AlbumTitle = AlbumTitle,
            CoverUrl = CoverUrl,
            Duration = Duration
        };

        return RemotePlayback.PlayOnDeviceAsync(device, request);
    }
}
