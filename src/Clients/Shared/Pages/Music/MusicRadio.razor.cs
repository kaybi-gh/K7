using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Domain.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.PersonRoles;
using Microsoft.AspNetCore.Components;
using MudBlazor;

using K7.Shared.Dtos.Entities.Metadatas.Files;

namespace K7.Clients.Shared.Pages.Music;

public partial class MusicRadio
{
    [Inject]
    private IAudioPlayerService Audio { get; set; } = default!;

    private RadioTypeInfo? _selectedRadio;
    private List<TrackViewModel> _tracks = [];
    private bool _loading;

    private static readonly List<RadioTypeInfo> _radioTypes =
    [
        new("Découverte", "Morceaux que vous n'avez jamais écoutés, proches de vos goûts.", MusicRadioType.Discovery, Icons.Material.Filled.Explore, Color.Info),
        new("Similaire", "Morceaux au son proche de ce que vous écoutez.", MusicRadioType.Sonic, Icons.Material.Filled.GraphicEq, Color.Primary),
        new("Tempo", "Un mix basé sur le BPM de vos morceaux préférés.", MusicRadioType.Tempo, Icons.Material.Filled.Speed, Color.Warning),
        new("Time Capsule", "Redécouvrez ce que vous écoutiez il y a un an.", MusicRadioType.TimeCapsule, Icons.Material.Filled.History, Color.Secondary),
        new("Nouveautés", "Les derniers morceaux ajoutés à votre bibliothèque.", MusicRadioType.RecentlyAdded, Icons.Material.Filled.NewReleases, Color.Success),
        new("Artiste", "Un mix centré sur un artiste et ses proches.", MusicRadioType.Artist, Icons.Material.Filled.Person, Color.Tertiary),
        new("Mood : Chill", "Ambiance détendue et calme.", MusicRadioType.Mood, Icons.Material.Filled.SelfImprovement, Color.Info, "chill"),
        new("Mood : Énergique", "Des morceaux qui bougent.", MusicRadioType.Mood, Icons.Material.Filled.ElectricBolt, Color.Error, "energetic"),
        new("Mood : Happy", "De la bonne humeur.", MusicRadioType.Mood, Icons.Material.Filled.SentimentVerySatisfied, Color.Warning, "happy"),
        new("Mood : Focus", "Concentration et productivité.", MusicRadioType.Mood, Icons.Material.Filled.Psychology, Color.Primary, "focus"),
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

    private async Task OnTrackClick(TableRowClickEventArgs<TrackViewModel> args)
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
        var artist = track.PersonRoles?
            .OfType<LiteMusicArtistRoleDto>()
            .FirstOrDefault(r => r.Person is not null);

        return new()
        {
            Id = track.Id,
            IndexedFileId = track.IndexedFiles?.FirstOrDefault()?.Id,
            Title = track.Title ?? "Sans titre",
            ArtistName = artist?.Person?.Name,
            ArtistPersonId = artist?.Person?.Id,
            AlbumTitle = null,
            Genre = track.Genres?.FirstOrDefault(),
            CoverUrl = K7ServerService.GetAbsoluteUri(
                track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?
                    .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
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
        ArtistPersonId = t.ArtistPersonId,
        AlbumTitle = t.AlbumTitle,
        Genre = t.Genre,
        CoverUrl = t.CoverUrl,
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
        public Guid? ArtistPersonId { get; init; }
        public string? AlbumTitle { get; init; }
        public string? Genre { get; init; }
        public string? CoverUrl { get; init; }
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
        Color Color,
        string? MoodPreset = null);
}
