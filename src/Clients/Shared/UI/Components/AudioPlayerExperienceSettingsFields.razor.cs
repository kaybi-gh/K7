using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components;

public partial class AudioPlayerExperienceSettingsFields
{
    private static readonly string[] EqFrequencyLabels =
        ["31", "62", "125", "250", "500", "1k", "2k", "4k", "8k", "16k"];

    private static readonly Dictionary<string, double[]> EqPresetBands = new()
    {
        ["flat"] = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        ["bass-boost"] = [6, 5, 4, 2, 0, 0, 0, 0, 0, 0],
        ["treble-boost"] = [0, 0, 0, 0, 0, 1, 3, 5, 6, 7],
        ["vocal"] = [-2, -1, 0, 3, 5, 5, 3, 0, -1, -2],
        ["rock"] = [5, 4, 2, 0, -1, -1, 0, 2, 4, 5],
        ["electronic"] = [5, 4, 1, 0, -2, 0, 1, 3, 5, 5],
    };

    [Parameter] public AudioPlayerSettingsDto? Settings { get; set; }
    [Parameter] public EventCallback<AudioPlayerSettingsDto> SettingsChanged { get; set; }

    private void OnChanged(Action<AudioPlayerSettingsDto> apply)
    {
        if (Settings is null)
            return;

        apply(Settings);
        _ = SettingsChanged.InvokeAsync(Settings);
        StateHasChanged();
    }

    private void OnBandChanged(int index, double value)
    {
        if (Settings is null)
            return;

        Settings.EqBands[index] = value;
        Settings.EqPresetName = "custom";
        _ = SettingsChanged.InvokeAsync(Settings);
        StateHasChanged();
    }

    private void OnPresetChanged(string preset)
    {
        if (Settings is null)
            return;

        Settings.EqPresetName = preset;
        if (EqPresetBands.TryGetValue(preset, out var bands))
            Settings.EqBands = (double[])bands.Clone();

        _ = SettingsChanged.InvokeAsync(Settings);
    }

    private static string FormatEqBandValue(double value) =>
        value > 0 ? $"+{value:F1}" : $"{value:F1}";

    private static string FormatLufs(double value) => $"{value:F0} LUFS";

    private static string FormatPreampDb(double value) => $"{value:F1} dB";

    private static string FormatSkipSeconds(int value) => $"{value}s";

    private string FormatCrossfadeDuration(double value)
    {
        if (value > 0)
            return $"{value:F0}s";

        return Settings?.AdaptiveCrossfade == true
            ? L["CrossfadeAdaptiveAuto"]
            : L["CrossfadeDisabled"];
    }
}
