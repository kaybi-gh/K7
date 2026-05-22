using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsAudioPlayerPage : IDisposable
{
    private static readonly string[] EqFrequencyLabels =
        ["31", "62", "125", "250", "500", "1k", "2k", "4k", "8k", "16k"];

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

        SleepTimerService.TimerChanged += OnTimerChanged;
    }

    private void OnBandChanged(int index, double value)
    {
        _eqBands[index] = value;
        _eqPresetName = "custom";
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
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void StartSleepTimer(int minutes)
    {
        SleepTimerService.Start(SleepTimerMode.Duration, TimeSpan.FromMinutes(minutes));
    }

    private void StartSleepTimerEndOfTrack()
    {
        SleepTimerService.Start(SleepTimerMode.EndOfTrack);
    }

    private void CancelSleepTimer()
    {
        SleepTimerService.Cancel();
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
        return $"{remaining.Minutes}m {remaining.Seconds}s";
    }

    private void OnTimerChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        SleepTimerService.TimerChanged -= OnTimerChanged;
    }
}
