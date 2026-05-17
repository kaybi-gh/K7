using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.PersonRoles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using K7.Shared.Dtos.Entities.Metadatas.Files;

namespace K7.Clients.Shared.UI.Pages.Music;

public partial class MusicRadio
{
    [Inject]
    private IAudioPlayerService Audio { get; set; } = default!;

    private RadioTypeInfo? _selectedRadio;
    private List<TrackViewModel> _tracks = [];
    private bool _loading;

    private static readonly List<RadioTypeInfo> _radioTypes =
    [
        new("D�couverte", "Morceaux que vous n'avez jamais �cout�s, proches de vos go�ts.", MusicRadioType.Discovery, "compass", "info"),
        new("Similaire", "Morceaux au son proche de ce que vous �coutez.", MusicRadioType.Sonic, "waves", "primary"),
        new("Tempo", "Un mix bas� sur le BPM de vos morceaux pr�f�r�s.", MusicRadioType.Tempo, "gauge", "warning"),
        new("Time Capsule", "Red�couvrez ce que vous �coutiez il y a un an.", MusicRadioType.TimeCapsule, "clock-clockwise", "secondary"),
        new("Nouveaut�s", "Les derniers morceaux ajout�s � votre biblioth�que.", MusicRadioType.RecentlyAdded, "sparkle", "success"),
        new("Artiste", "Un mix centr� sur un artiste et ses proches.", MusicRadioType.Artist, "user", "tertiary"),
        new("Mood : Chill", "Ambiance d�tendue et calme.", MusicRadioType.Mood, "peace", "info", "chill"),
        new("Mood : �nergique", "Des morceaux qui bougent.", MusicRadioType.Mood, "lightning", "error", "energetic"),
        new("Mood : Happy", "De la bonne humeur.", MusicRadioType.Mood, "smiley", "warning", "happy"),
        new("Mood : Focus", "Concentration et productivit�.", MusicRadioType.Mood, "brain", "primary", "focus"),
    ];

    private async Task PlayRadioAsync(RadioTypeInfo radio)
    {
        _selectedRadio = radio;
        _loading = true;
        _tracks = [];
        StateHasChanged();

        var typeName = radio.Type.ToString();
        var results = await K7ServerService.GetMusicRadioAsync(typeName, moodPreset: radio.MoodPreset);

        if (results is not null)
        {
            _tracks = results
                .OfType<MusicTrackDto>()
                .Select(ToViewModel)
                .ToList();

            // Auto-play the mix
            var queueItems = _tracks
                .Where(t => t.IndexedFileId.HasValue)
                .Select(BuildQueueItem)
                .ToList();

            if (queueItems.Count > 0)
                await Audio.PlayTracksAsync(queueItems, 0);
        }

        _loading = false;
    }

    private async Task OnTrackClick(K7.Clients.Shared.UI.Components.TableRowClickEventArgs<TrackViewModel> args)
    {
        var track = args.Item;
        if (track is null) return;

        var queueItems = _tracks
            .Where(t => t.IndexedFileId.HasValue)
            .Select(BuildQueueItem)
            .ToList();

        var index = queueItems.FindIndex(q => q.MediaId == track.Id);
        await Audio.PlayTracksAsync(queueItems, index >= 0 ? index : 0);
    }

    private TrackViewModel ToViewModel(MusicTrackDto track)
    {
        return new()
        {
            Id = track.Id,
            IndexedFileId = track.IndexedFiles?.FirstOrDefault()?.Id,
            Title = track.Title ?? S["Untitled"],
            ArtistName = track.ArtistName,
            ArtistId = track.ArtistId,
            AlbumTitle = null,
            Genre = track.Genres?.FirstOrDefault(),
            CoverUrl = ApiClient.GetAbsoluteUri(
                (track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
                    ?? track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster))?
                    .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
            CoverDominantColor = (track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
                ?? track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster))?.DominantColor,
            Duration = (track.IndexedFiles?.FirstOrDefault()?.FileMetadata as AudioFileMetadataDto)?.Duration.TotalSeconds ?? 0,
            Bpm = track.Bpm,
            MusicalKey = track.MusicalKey,
            Energy = track.Energy,
            IsPlaying = Audio.CurrentTrack?.MediaId == track.Id
        };
    }

    private static AudioQueueItem BuildQueueItem(TrackViewModel t) => new()
    {
        IndexedFileId = t.IndexedFileId!.Value,
        MediaId = t.Id,
        Title = t.Title,
        Artist = t.ArtistName,
        ArtistId = t.ArtistId,
        AlbumTitle = t.AlbumTitle,
        Genre = t.Genre,
        CoverUrl = t.CoverUrl,
        CoverDominantColor = t.CoverDominantColor,
        Duration = t.Duration,
        Bpm = t.Bpm,
        MusicalKey = t.MusicalKey,
        Energy = t.Energy
    };

    private static string FormatTime(double seconds)
    {
        if (seconds <= 0) return "";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? $"{ts.Hours:0}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes:0}:{ts.Seconds:00}";
    }

    internal sealed record TrackViewModel
    {
        public Guid Id { get; init; }
        public Guid? IndexedFileId { get; init; }
        public required string Title { get; init; }
        public string? ArtistName { get; init; }
        public Guid? ArtistId { get; init; }
        public string? AlbumTitle { get; init; }
        public string? Genre { get; init; }
        public string? CoverUrl { get; init; }
        public string? CoverDominantColor { get; init; }
        public double Duration { get; init; }
        public double? Bpm { get; init; }
        public string? MusicalKey { get; init; }
        public double? Energy { get; init; }
        public bool IsPlaying { get; init; }
    }

    internal sealed record RadioTypeInfo(
        string Title,
        string Description,
        MusicRadioType Type,
        string Icon,
        string Color,
        string? MoodPreset = null);
}
