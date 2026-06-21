using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Music;

public partial class MusicRadioPresets
{
    [Inject] private IServerInfoService ServerInfo { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private IAudioPlayerService Audio { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public string Class { get; set; } = string.Empty;

    private IReadOnlyList<RadioPresetInfo> Presets { get; set; } = [];

    protected override void OnInitialized()
    {
        Presets =
        [
            new(L["PresetDiscovery"], L["PresetDiscoveryDesc"], MusicRadioType.Discovery, Phosphor.Compass, "--radio-tone: var(--color-info);"),
            new(L["PresetSonic"], L["PresetSonicDesc"], MusicRadioType.Sonic, Phosphor.Waves, "--radio-tone: #6B8FC4;"),
            new(L["PresetTempo"], L["PresetTempoDesc"], MusicRadioType.Tempo, Phosphor.Gauge, "--radio-tone: var(--color-warning);"),
            new(L["PresetTimeCapsule"], L["PresetTimeCapsuleDesc"], MusicRadioType.TimeCapsule, Phosphor.ClockCounterClockwise, "--radio-tone: #8B7AA8;"),
            new(L["PresetRecentlyAdded"], L["PresetRecentlyAddedDesc"], MusicRadioType.RecentlyAdded, Phosphor.Sparkle, "--radio-tone: var(--color-success);"),
            new(L["PresetArtist"], L["PresetArtistDesc"], MusicRadioType.Artist, Phosphor.User, "--radio-tone: var(--color-accent);"),
        ];
    }

    private async Task PlayRadioAsync(RadioPresetInfo radio)
    {
        var results = await ServerInfo.GetMusicRadioAsync(radio.Type.ToString());
        if (results is null) return;

        var queueItems = results
            .OfType<MusicTrackDto>()
            .Select(ToQueueItem)
            .Where(q => q is not null)
            .Cast<AudioQueueItem>()
            .ToList();

        if (queueItems.Count > 0)
            await Audio.PlayTracksAsync(queueItems, 0);
    }

    private AudioQueueItem? ToQueueItem(MusicTrackDto track)
    {
        var indexedFileId = track.IndexedFiles?.FirstOrDefault()?.Id;
        if (indexedFileId is null)
            return null;

        var cover = track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
            ?? track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster);

        return new AudioQueueItem
        {
            IndexedFileId = indexedFileId.Value,
            MediaId = track.Id,
            Title = track.Title ?? S["Untitled"],
            Artist = track.ArtistName,
            ArtistId = track.ArtistId,
            AlbumTitle = null,
            Genre = track.Genres?.FirstOrDefault(),
            CoverUrl = ApiClient.GetAbsoluteUri(cover?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
            CoverDominantColor = cover?.DominantColor,
            Duration = (track.IndexedFiles?.FirstOrDefault()?.FileMetadata as AudioFileMetadataDto)?.Duration.TotalSeconds ?? 0,
            Bpm = track.Bpm,
            MusicalKey = track.MusicalKey,
            Energy = track.Energy
        };
    }

    internal sealed record RadioPresetInfo(
        string Title,
        string Description,
        MusicRadioType Type,
        string Icon,
        string BackgroundStyle);
}
