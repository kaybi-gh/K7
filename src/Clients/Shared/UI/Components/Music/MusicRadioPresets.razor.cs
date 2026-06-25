using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Music;

public partial class MusicRadioPresets
{
    [Inject] private IServerInfoService ServerInfo { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private IAudioPlayerService Audio { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;
    [Inject] private IServerPreferencesService ServerPreferences { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public Guid[]? LibraryIds { get; set; }
    [Parameter] public Guid[]? LibraryGroupIds { get; set; }

    private IReadOnlyList<RadioPresetInfo> Presets { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
        var musicIntelligenceAvailable = false;
        try
        {
            var status = await ServerPreferences.GetMusicIntelligenceStatusAsync();
            musicIntelligenceAvailable = status.IsAvailable;
        }
        catch
        {
            musicIntelligenceAvailable = false;
        }

        var presets = new List<RadioPresetInfo>
        {
            new(L["PresetDiscovery"], L["PresetDiscoveryDesc"], Phosphor.Compass, "--radio-tone: var(--color-info);", MusicRadioType.Discovery),
            new(L["PresetTimeCapsule"], L["PresetTimeCapsuleDesc"], Phosphor.ClockCounterClockwise, "--radio-tone: #8B7AA8;", MusicRadioType.TimeCapsule),
            new(L["PresetRecentlyAdded"], L["PresetRecentlyAddedDesc"], Phosphor.Sparkle, "--radio-tone: var(--color-success);", MusicRadioType.RecentlyAdded),
        };

        if (musicIntelligenceAvailable)
        {
            presets.Insert(1, new(L["PresetDiscoveryAi"], L["PresetDiscoveryAiDesc"], Phosphor.Sparkle, "--radio-tone: #7A9E7E;", MusicRadioType.DiscoveryAi));
            presets.Add(new(L["PresetAmbiance"], L["PresetAmbianceDesc"], Phosphor.MoonStars, "--radio-tone: var(--color-warning);", Action: RadioPresetAction.Ambiance));
            presets.Add(new(L["PresetSonicPath"], L["PresetSonicPathDesc"], Phosphor.Path, "--radio-tone: #9A8BB8;", Action: RadioPresetAction.SonicPath));
            presets.Add(new(L["PresetIntelligentSearch"], L["PresetIntelligentSearchDesc"], Phosphor.MagnifyingGlass, "--radio-tone: #6B8FA3;", Action: RadioPresetAction.IntelligentSearch));
        }

        Presets = presets;
    }

    private async Task OnPresetClickedAsync(RadioPresetInfo radio)
    {
        switch (radio.Action)
        {
            case RadioPresetAction.Ambiance:
                await OpenAmbianceDialogAsync();
                return;
            case RadioPresetAction.SonicPath:
                await OpenSonicPathDialogAsync();
                return;
            case RadioPresetAction.IntelligentSearch:
                await OpenIntelligentSearchDialogAsync();
                return;
            default:
                await PlayRadioAsync(radio);
                return;
        }
    }

    private async Task OpenAmbianceDialogAsync()
    {
        var parameters = new K7DialogParameters<AmbianceRadioDialog>
        {
            { x => x.LibraryIds, LibraryIds },
            { x => x.LibraryGroupIds, LibraryGroupIds }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<AmbianceRadioDialog>(L["PresetAmbiance"], parameters, options);
    }

    private async Task OpenSonicPathDialogAsync()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<SonicPathDialog>(L["PresetSonicPath"], null, options);
    }

    private async Task OpenIntelligentSearchDialogAsync()
    {
        var parameters = new K7DialogParameters<IntelligentSearchDialog>
        {
            { x => x.LibraryIds, LibraryIds },
            { x => x.LibraryGroupIds, LibraryGroupIds }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<IntelligentSearchDialog>(L["PresetIntelligentSearch"], parameters, options);
    }

    private async Task PlayRadioAsync(RadioPresetInfo radio)
    {
        if (radio.Type is null)
            return;

        var results = await ServerInfo.GetMusicRadioAsync(
            radio.Type.Value.ToString(),
            LibraryIds,
            LibraryGroupIds,
            moodPreset: radio.MoodPreset,
            moodCentroidIndex: radio.MoodCentroidIndex);

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

    private enum RadioPresetAction
    {
        Play,
        Ambiance,
        SonicPath,
        IntelligentSearch
    }

    private sealed record RadioPresetInfo(
        string Title,
        string Description,
        string Icon,
        string BackgroundStyle,
        MusicRadioType? Type = null,
        RadioPresetAction Action = RadioPresetAction.Play,
        string? MoodPreset = null,
        int? MoodCentroidIndex = null);
}
