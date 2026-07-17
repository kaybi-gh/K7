using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class AmbianceRadioDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;
    [Parameter] public Guid[]? LibraryIds { get; set; }
    [Parameter] public Guid[]? LibraryGroupIds { get; set; }

    private List<MusicMoodPresetDto> _presets = [];
    private bool _loading = true;
    private bool _playing;
    private string? _selectedPresetKey;
    private MusicMoodPresetDto? _selectedPreset;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _presets = (await ServerPreferences.GetMusicMoodPresetsAsync()).ToList();
            if (_presets.Count > 0)
            {
                _selectedPreset = _presets[0];
                _selectedPresetKey = GetPresetKey(_presets[0]);
            }
        }
        catch
        {
            _presets = [];
        }

        _loading = false;
    }

    private void Cancel() => Dialog.Close(K7DialogResult.Cancel());

    private void OnPresetKeyChanged(string? key)
    {
        _selectedPresetKey = key;
        if (string.IsNullOrWhiteSpace(key))
        {
            _selectedPreset = null;
            return;
        }

        var parts = key.Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var centroidIndex))
        {
            _selectedPreset = null;
            return;
        }

        _selectedPreset = _presets.FirstOrDefault(p => p.MoodKey == parts[0] && p.CentroidIndex == centroidIndex);
    }

    private async Task PlayAsync()
    {
        if (_selectedPreset is null)
            return;

        _playing = true;
        try
        {
            var started = await MusicRadio.StartAsync(new MusicRadioRequest
            {
                RadioType = MusicRadioType.Mood.ToString(),
                Title = GetPresetLabel(_selectedPreset),
                LibraryIds = LibraryIds,
                LibraryGroupIds = LibraryGroupIds,
                MoodPreset = _selectedPreset.MoodKey,
                MoodCentroidIndex = _selectedPreset.CentroidIndex
            });

            if (!started)
            {
                Snackbar.Add(L["EmptyRadio"], K7Severity.Info);
                return;
            }

            Dialog.Close(K7DialogResult.Ok(true));
        }
        finally
        {
            _playing = false;
        }
    }

    private static string GetPresetKey(MusicMoodPresetDto preset) => $"{preset.MoodKey}:{preset.CentroidIndex}";

    private string GetPresetLabel(MusicMoodPresetDto preset)
    {
        var moodLabel = L[$"Mood_{preset.MoodKey}"];
        return preset.CentroidIndex > 0
            ? string.Format(L["MoodVariant"], moodLabel, preset.CentroidIndex + 1)
            : moodLabel;
    }
}
