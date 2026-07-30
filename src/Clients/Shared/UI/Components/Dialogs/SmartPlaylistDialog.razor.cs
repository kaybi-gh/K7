using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class SmartPlaylistDialog
{
    private enum AiPlaylistMode
    {
        Prompt,
        Sonic,
        Lyrics
    }

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    private string _prompt = string.Empty;
    private bool _loading;
    private bool _instantPlaylistAvailable;
    private AiPlaylistMode _mode = AiPlaylistMode.Sonic;
    private string[] _examples = SonicExamples;

    private static readonly string[] PromptExamples =
    [
        "Chill jazz for a rainy Sunday",
        "Upbeat 90s dance party",
        "Focus playlist, instrumental only"
    ];

    private static readonly string[] SonicExamples =
    [
        "calm piano, soft drums",
        "upbeat indie pop guitars",
        "dark electronic synthwave"
    ];

    private static readonly string[] LyricsExamples =
    [
        "lost love, dancing in the rain",
        "city nights neon lights",
        "summer road trip freedom"
    ];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var status = await ServerPreferences.GetMusicIntelligenceStatusAsync();
            _instantPlaylistAvailable = status.InstantPlaylistAvailable;
        }
        catch
        {
            _instantPlaylistAvailable = false;
        }

        if (_instantPlaylistAvailable)
        {
            _mode = AiPlaylistMode.Prompt;
            _examples = PromptExamples;
        }
        else
        {
            _mode = AiPlaylistMode.Sonic;
            _examples = SonicExamples;
        }
    }

    private void Cancel() => Dialog.Close(K7DialogResult.Cancel());

    private void OnModeChanged(AiPlaylistMode mode)
    {
        if (mode == AiPlaylistMode.Prompt && !_instantPlaylistAvailable)
            mode = AiPlaylistMode.Sonic;

        _mode = mode;
        _examples = mode switch
        {
            AiPlaylistMode.Sonic => SonicExamples,
            AiPlaylistMode.Lyrics => LyricsExamples,
            _ => PromptExamples
        };
    }

    private void UseExample(string example) => _prompt = example;

    private string GetPlaceholder() => _mode switch
    {
        AiPlaylistMode.Sonic => L["SonicPlaceholder"],
        AiPlaylistMode.Lyrics => L["LyricsPlaceholder"],
        _ => L["PromptPlaceholder"]
    };

    private string GetHelperText() => _mode switch
    {
        AiPlaylistMode.Sonic => L["SonicHint"],
        AiPlaylistMode.Lyrics => L["LyricsHint"],
        _ => L["PromptHint"]
    };

    private async Task Generate()
    {
        if (string.IsNullOrWhiteSpace(_prompt) || _loading)
            return;

        if (_mode == AiPlaylistMode.Prompt && !_instantPlaylistAvailable)
            return;

        _loading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            var trackIds = _mode switch
            {
                AiPlaylistMode.Sonic => await MusicIntelligence.SearchTracksBySonicTextAsync(_prompt.Trim()),
                AiPlaylistMode.Lyrics => await MusicIntelligence.SearchTracksByLyricsAsync(_prompt.Trim()),
                _ => await MusicIntelligence.CreateSmartPlaylistAsync(_prompt.Trim())
            };

            if (trackIds.Count == 0)
            {
                Snackbar.Add(L["NoResults"], K7Severity.Warning);
                return;
            }

            var tracks = await IntelligentSearchHelper.LoadScopedTracksAsync(
                MediaService,
                trackIds,
                libraryIds: null,
                libraryGroupIds: null);

            var queueItems = MusicTrackQueueMapper.ToQueueItems(tracks, ApiClient, S["Untitled"]);

            if (queueItems.Count > 0)
            {
                await Audio.PlayTracksAsync(queueItems, 0);
                Snackbar.Add(string.Format(L["Playing"], queueItems.Count), K7Severity.Info);
                Dialog.Close(K7DialogResult.Ok(true));
            }
            else
            {
                Snackbar.Add(L["NoResults"], K7Severity.Warning);
            }
        }
        catch
        {
            Snackbar.Add(L["Error"], K7Severity.Error);
        }
        finally
        {
            _loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }
}
