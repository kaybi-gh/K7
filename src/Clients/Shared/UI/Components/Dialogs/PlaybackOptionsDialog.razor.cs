using System.Globalization;
using K7.Shared;
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

    private static string GetAudioTrackLabel(AudioFileTrackDto? track)
    {
        if (track is null) return "";

        var language = GetTranslatedLanguageName(track.Language ?? "und");
        var channels = track.ChannelLayout?.Split('(')[0].Trim();
        var details = !string.IsNullOrEmpty(channels)
            ? $"{track.Codec} {channels}"
            : track.Codec;

        if (!string.IsNullOrEmpty(track.Name)
            && !string.Equals(track.Name, track.Language, StringComparison.OrdinalIgnoreCase)
            && !IsLanguageCode(track.Name)
            && !track.Name.Contains(track.Codec ?? "", StringComparison.OrdinalIgnoreCase))
        {
            return $"{language} - {track.Name} ({details})";
        }

        return $"{language} ({details})";
    }

    private static string GetSubtitleTrackLabel(SubtitleFileTrackDto? track)
    {
        if (track is null) return "";

        var language = GetTranslatedLanguageName(track.Language ?? "und");
        var type = track.IsHearingImpaired ? "SDH"
            : track.IsForced ? "Forced"
            : "Full";
        return $"{language} - {type} ({track.Codec})";
    }

    private static string GetTranslatedLanguageName(string code)
    {
        if (string.IsNullOrEmpty(code) || code == "und")
            return code;

        try
        {
            var culture = CultureInfo.GetCultureInfo(code);
            if (!string.IsNullOrEmpty(culture.DisplayName) && culture.DisplayName != code)
                return char.ToUpper(culture.DisplayName[0]) + culture.DisplayName[1..];
        }
        catch
        {
            // CultureInfo doesn't recognize this code
        }

        return SupportedLanguages.GetDisplayLabel(code);
    }

    private static bool IsLanguageCode(string value)
    {
        return value.Length is 2 or 3
            && value.All(char.IsLetter)
            && SupportedLanguages.FindByCode(value) is not null;
    }
}

public class PlaybackOptionsResult
{
    public IndexedFileDto? SelectedFile { get; set; }
    public AudioFileTrackDto? AudioTrack { get; set; }
    public SubtitleFileTrackDto? SubtitleTrack { get; set; }
}