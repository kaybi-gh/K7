using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class DownloadOptionsDialog
{
    [CascadingParameter] IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public required VideoFileMetadataDto VideoMetadata { get; set; }

    private VideoFileMetadataDto? videoMetadata => VideoMetadata;

    public AudioFileTrackDto? SelectedAudioTrack { get; set; }
    public SubtitleFileTrackDto? SelectedSubtitleTrack { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        SelectedAudioTrack = VideoMetadata.AudioTracks?.FirstOrDefault(x => x.IsDefault)
                             ?? VideoMetadata.AudioTracks?.FirstOrDefault();
        SelectedSubtitleTrack = VideoMetadata.SubtitleTracks?.FirstOrDefault(x => x.IsDefault);
    }

    private void Cancel()
    {
        Dialog.Cancel();
    }

    private void Confirm()
    {
        var result = new DownloadOptionsResult
        {
            AudioTrack = SelectedAudioTrack,
            SubtitleTrack = SelectedSubtitleTrack
        };
        Dialog.Close(K7DialogResult.Ok(result));
    }
}

public sealed class DownloadOptionsResult
{
    public AudioFileTrackDto? AudioTrack { get; init; }
    public SubtitleFileTrackDto? SubtitleTrack { get; init; }
}
