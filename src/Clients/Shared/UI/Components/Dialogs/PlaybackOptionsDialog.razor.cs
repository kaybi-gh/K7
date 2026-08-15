using K7.Clients.Shared.Helpers;
using K7.Shared;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class PlaybackOptionsDialog
{
    [CascadingParameter] IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public required MovieDto Movie { get; set; }
    [Parameter] public Guid? InitialFileId { get; set; }
    [Parameter] public Guid? InitialRemoteFileId { get; set; }

    [Inject] private IFederationService FederationService { get; set; } = default!;
    [Inject] private ILogger<PlaybackOptionsDialog> Logger { get; set; } = default!;

    private List<PlaybackReleaseOption> _releases = [];
    private PlaybackReleaseOption? _selectedRelease;
    private PlaybackReleaseOption? SelectedRelease
    {
        get => _selectedRelease;
        set
        {
            if (ReferenceEquals(_selectedRelease, value))
                return;

            _selectedRelease = value;
            SelectedAudioTrack = videoMetadata?.AudioTracks?.FirstOrDefault(x => x.IsDefault)
                ?? videoMetadata?.AudioTracks?.FirstOrDefault();
            SelectedSubtitleTrack = videoMetadata?.SubtitleTracks?.FirstOrDefault(x => x.IsDefault);
        }
    }

    private VideoFileMetadataDto? videoMetadata => SelectedRelease?.File?.FileMetadata as VideoFileMetadataDto;
    private bool ShowFilePicker => _releases.Count > 1;
    private bool ShowSource => _releases.Any(r => r.IsRemote) && _releases.Any(r => !r.IsRemote);

    public AudioFileTrackDto? SelectedAudioTrack { get; set; }
    public SubtitleFileTrackDto? SelectedSubtitleTrack { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        BuildReleases();
        SelectInitialRelease();
        LoadRemoteDetailsAsync().FireAndForget(Logger);
    }

    private void BuildReleases()
    {
        _releases = [];

        foreach (var file in Movie.IndexedFiles ?? [])
        {
            _releases.Add(new PlaybackReleaseOption
            {
                Id = file.Id,
                File = file
            });
        }

        foreach (var remote in Movie.RemoteIndexedFiles ?? [])
        {
            _releases.Add(new PlaybackReleaseOption
            {
                Id = remote.Id,
                Remote = remote
            });
        }
    }

    private void SelectInitialRelease()
    {
        SelectedRelease =
            _releases.FirstOrDefault(r => r.IsRemote && r.Id == InitialRemoteFileId)
            ?? _releases.FirstOrDefault(r => !r.IsRemote && r.Id == InitialFileId)
            ?? _releases.FirstOrDefault();
    }

    private async Task LoadRemoteDetailsAsync()
    {
        var remotes = _releases.Where(r => r.IsRemote && r.File is null).ToList();
        if (remotes.Count == 0)
            return;

        foreach (var release in remotes)
        {
            try
            {
                var details = await FederationService.GetRemoteFileDetailsAsync(release.Id);
                if (details is null)
                    continue;

                release.File = details;
                if (_selectedRelease?.Id == release.Id)
                {
                    SelectedAudioTrack ??= videoMetadata?.AudioTracks?.FirstOrDefault(x => x.IsDefault)
                        ?? videoMetadata?.AudioTracks?.FirstOrDefault();
                    SelectedSubtitleTrack ??= videoMetadata?.SubtitleTracks?.FirstOrDefault(x => x.IsDefault);
                }

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to load federated file details {RemoteFileId}", release.Id);
            }
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
            SelectedFile = SelectedRelease?.File,
            RemoteFile = SelectedRelease?.Remote,
            AudioTrack = SelectedAudioTrack,
            SubtitleTrack = SelectedSubtitleTrack
        };
        Dialog.Close(K7DialogResult.Ok(result));
    }

    private string GetReleaseLabel(PlaybackReleaseOption? release)
    {
        if (release is null)
            return "";

        var source = ShowSource
            ? (release.IsRemote ? L["Federated"].Value : L["Local"].Value)
            : null;
        return PlaybackReleaseLabelHelper.Format(release.File, release.Remote, source);
    }

    private static string GetAudioTrackLabel(AudioFileTrackDto? track) =>
        AudioTrackDisplayHelper.FormatLabel(track);

    private static string GetSubtitleTrackLabel(SubtitleFileTrackDto? track)
    {
        var type = track is { IsHearingImpaired: true } ? "SDH"
            : track is { IsForced: true } ? "Forced"
            : "Full";
        return AudioTrackDisplayHelper.FormatSubtitleLabel(track, type);
    }
}

public sealed class PlaybackReleaseOption
{
    public required Guid Id { get; init; }
    public IndexedFileDto? File { get; set; }
    public RemoteIndexedFileDto? Remote { get; init; }
    public bool IsRemote => Remote is not null;
}

public class PlaybackOptionsResult
{
    public IndexedFileDto? SelectedFile { get; set; }
    public RemoteIndexedFileDto? RemoteFile { get; set; }
    public AudioFileTrackDto? AudioTrack { get; set; }
    public SubtitleFileTrackDto? SubtitleTrack { get; set; }
}
