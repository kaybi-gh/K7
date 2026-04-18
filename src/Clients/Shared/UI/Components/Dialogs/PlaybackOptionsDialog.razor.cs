using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class PlaybackOptionsDialog
{
    [CascadingParameter] IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public required MovieDto Movie { get; set; }
    [Parameter] public Guid? InitialFileId { get; set; }

    private IndexedFileDto? _selectedFile;
    private IndexedFileDto? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (_selectedFile != value)
            {
                _selectedFile = value;
                // Auto-select defaults when file changes
                SelectedAudioTrack = videoMetadata?.AudioTracks?.FirstOrDefault(x => x.IsDefault) ?? videoMetadata?.AudioTracks?.FirstOrDefault();
                SelectedSubtitleTrack = videoMetadata?.SubtitleTracks?.FirstOrDefault(x => x.IsDefault);
            }
        }
    }

    private VideoFileMetadataDto? videoMetadata => SelectedFile?.FileMetadata as VideoFileMetadataDto;

    public AudioFileTrackDto? SelectedAudioTrack { get; set; }
    public SubtitleFileTrackDto? SelectedSubtitleTrack { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        if (Movie.IndexedFiles != null)
        {
            SelectedFile = Movie.IndexedFiles.FirstOrDefault(f => f.Id == InitialFileId) ?? Movie.IndexedFiles.FirstOrDefault();
        }
    }

    private void Cancel()
    {
        Dialog.Cancel();
    }

    private void Play()
    {
        var result = new PlaybackOptionsResult
        {
            SelectedFile = SelectedFile,
            AudioTrack = SelectedAudioTrack,
            SubtitleTrack = SelectedSubtitleTrack
        };
        Dialog.Close(K7DialogResult.Ok(result));
    }
}

public class PlaybackOptionsResult
{
    public IndexedFileDto? SelectedFile { get; set; }
    public AudioFileTrackDto? AudioTrack { get; set; }
    public SubtitleFileTrackDto? SubtitleTrack { get; set; }
}