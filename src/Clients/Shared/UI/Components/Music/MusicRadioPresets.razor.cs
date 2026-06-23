using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Music;

public partial class MusicRadioPresets
{
    [Inject] private IServerInfoService ServerInfo { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private IAudioPlayerService Audio { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public Guid[]? LibraryIds { get; set; }
    [Parameter] public Guid[]? LibraryGroupIds { get; set; }

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
        ];
    }

    private async Task PlayRadioAsync(RadioPresetInfo radio)
    {
        var results = await ServerInfo.GetMusicRadioAsync(
            radio.Type.ToString(),
            LibraryIds,
            LibraryGroupIds);

        var queueItems = results?
            .OfType<MusicTrackDto>()
            .Select(t => MusicTrackQueueMapper.ToQueueItem(t, ApiClient, S["Untitled"]))
            .Where(q => q is not null)
            .Cast<AudioQueueItem>()
            .ToList();

        if (queueItems is not { Count: > 0 })
        {
            Snackbar.Add(L["EmptyRadio"], K7Severity.Info);
            return;
        }

        await Audio.PlayRadioAsync(queueItems, radio.Title);
    }

    internal sealed record RadioPresetInfo(
        string Title,
        string Description,
        MusicRadioType Type,
        string Icon,
        string BackgroundStyle);
}
