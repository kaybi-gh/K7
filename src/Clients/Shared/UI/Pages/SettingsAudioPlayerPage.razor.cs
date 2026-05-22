using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsAudioPlayerPage
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

    private bool _loudnessEnabled;
    private double _targetLufs;
    private double _preampDb;
    private bool _limiterEnabled;
    private bool _eqEnabled;
    private string _eqPresetName = "flat";
    private double[] _eqBands = new double[10];
    private double _crossfadeDuration;
    private bool _adaptiveCrossfade;
    private bool _autoplayEnabled;
    private int _streamingQualityWifi;
    private int _streamingQualityMobile;
    private bool _downmixToStereo;
    private bool _showFullscreenOnPlay;
    private bool _keepScreenOn;
    private int _skipBackSeconds;
    private int _skipForwardSeconds;

    protected override void OnInitialized()
    {
        _loudnessEnabled = AudioPlayerService.LoudnessEnabled;
        _targetLufs = AudioPlayerService.LoudnessTargetLufs;
        _preampDb = AudioPlayerService.LoudnessPreampDb;
        _limiterEnabled = AudioPlayerService.LimiterEnabled;
        _eqEnabled = AudioPlayerService.EqEnabled;
        _eqPresetName = AudioPlayerService.EqPresetName ?? "flat";
        _eqBands = (double[])AudioPlayerService.EqBands.Clone();
        _crossfadeDuration = AudioPlayerService.CrossfadeDuration;
        _adaptiveCrossfade = AudioPlayerService.AdaptiveCrossfade;
        _autoplayEnabled = AutoplayService.Enabled;
        _streamingQualityWifi = DeviceStorage.Get(PreferenceKeys.STREAMING_QUALITY_WIFI, 0);
        _streamingQualityMobile = DeviceStorage.Get(PreferenceKeys.STREAMING_QUALITY_MOBILE, 0);
        _downmixToStereo = DeviceStorage.Get(PreferenceKeys.DOWNMIX_TO_STEREO, false);
        _showFullscreenOnPlay = AudioPlayerService.ShowFullscreenOnPlay;
        _keepScreenOn = AudioPlayerService.KeepScreenOn;
        _skipBackSeconds = AudioPlayerService.SkipBackSeconds;
        _skipForwardSeconds = AudioPlayerService.SkipForwardSeconds;
    }

    private void OnBandChanged(int index, double value)
    {
        _eqBands[index] = value;
        _eqPresetName = "custom";
    }

    private void OnPresetChanged(string preset)
    {
        _eqPresetName = preset;
        if (EqPresetBands.TryGetValue(preset, out var bands))
            _eqBands = (double[])bands.Clone();
    }

    private Task SaveAsync()
    {
        AudioPlayerService.SetLoudnessEnabled(_loudnessEnabled);
        AudioPlayerService.SetLoudnessTargetLufs(_targetLufs);
        AudioPlayerService.SetLoudnessPreampDb(_preampDb);
        AudioPlayerService.SetLimiterEnabled(_limiterEnabled);
        AudioPlayerService.SetEqEnabled(_eqEnabled);
        AudioPlayerService.SetEqBands(_eqBands);
        AudioPlayerService.SetEqPresetName(_eqPresetName);
        AudioPlayerService.SetCrossfadeDuration(_crossfadeDuration);

        if (_adaptiveCrossfade != AudioPlayerService.AdaptiveCrossfade)
            AudioPlayerService.ToggleAdaptiveCrossfade();

        AutoplayService.SetEnabled(_autoplayEnabled);

        DeviceStorage.Set(PreferenceKeys.STREAMING_QUALITY_WIFI, _streamingQualityWifi);
        DeviceStorage.Set(PreferenceKeys.STREAMING_QUALITY_MOBILE, _streamingQualityMobile);
        DeviceStorage.Set(PreferenceKeys.DOWNMIX_TO_STEREO, _downmixToStereo);

        AudioPlayerService.SetShowFullscreenOnPlay(_showFullscreenOnPlay);
        AudioPlayerService.SetKeepScreenOn(_keepScreenOn);
        AudioPlayerService.SetSkipBackSeconds(_skipBackSeconds);
        AudioPlayerService.SetSkipForwardSeconds(_skipForwardSeconds);

        Snackbar.Add(L["Saved"], K7Severity.Success);

        return Task.CompletedTask;
    }

    private Task ResetAsync()
    {
        _loudnessEnabled = true;
        _targetLufs = -14.0;
        _preampDb = 0.0;
        _limiterEnabled = true;
        _eqEnabled = false;
        _eqPresetName = "flat";
        _eqBands = new double[10];
        _crossfadeDuration = 6.0;
        _adaptiveCrossfade = true;
        _autoplayEnabled = true;
        _streamingQualityWifi = 0;
        _streamingQualityMobile = 0;
        _downmixToStereo = false;
        _showFullscreenOnPlay = false;
        _keepScreenOn = false;
        _skipBackSeconds = 5;
        _skipForwardSeconds = 5;
        StateHasChanged();
        return Task.CompletedTask;
    }
}
