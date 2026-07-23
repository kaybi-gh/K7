using Android.Media.Audiofx;
using Log = Android.Util.Log;

namespace K7.Clients.MAUI.Platforms.Android.Services;

/// <summary>
/// Maps the shared 10-band EQ settings onto the device Equalizer AudioEffect
/// attached to an ExoPlayer audio session.
/// </summary>
internal sealed class AndroidAudioEqualizer : IDisposable
{
    private const string Tag = "K7-AudioEq";

    private static readonly double[] UiFrequenciesHz =
        [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    private Equalizer? _equalizer;
    private bool _enabled;
    private double[] _bands = new double[10];
    private bool _disposed;

    public void UpdateSettings(bool enabled, double[] bands)
    {
        _enabled = enabled;
        if (bands.Length >= 10)
            _bands = (double[])bands.Clone();

        Apply();
    }

    public void Attach(int audioSessionId)
    {
        if (_disposed)
            return;

        ReleaseEqualizer();

        if (audioSessionId == 0 || audioSessionId == AndroidX.Media3.Common.C.AudioSessionIdUnset)
            return;

        try
        {
            _equalizer = new Equalizer(0, audioSessionId);
            Apply();
            Log.Info(Tag, $"Equalizer attached to session {audioSessionId} ({_equalizer.NumberOfBands} bands)");
        }
        catch (Exception ex)
        {
            _equalizer = null;
            Log.Warn(Tag, $"Equalizer not available: {ex.Message}");
        }
    }

    public void Detach() => ReleaseEqualizer();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ReleaseEqualizer();
    }

    private void Apply()
    {
        if (_equalizer is null)
            return;

        try
        {
            if (!_enabled)
            {
                _equalizer.SetEnabled(false);
                return;
            }

            var range = _equalizer.GetBandLevelRange();
            if (range is null || range.Length < 2)
                return;

            var minLevel = range[0];
            var maxLevel = range[1];
            var bandCount = _equalizer.NumberOfBands;

            for (short band = 0; band < bandCount; band++)
            {
                var centerHz = _equalizer.GetCenterFreq(band) / 1000.0;
                var gainDb = InterpolateGainDb(centerHz);
                var millibels = (short)Math.Clamp((int)Math.Round(gainDb * 100), minLevel, maxLevel);
                _equalizer.SetBandLevel(band, millibels);
            }

            _equalizer.SetEnabled(true);
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Failed to apply EQ bands: {ex.Message}");
        }
    }

    private double InterpolateGainDb(double centerHz)
    {
        if (centerHz <= UiFrequenciesHz[0])
            return _bands[0];

        if (centerHz >= UiFrequenciesHz[^1])
            return _bands[^1];

        for (var i = 0; i < UiFrequenciesHz.Length - 1; i++)
        {
            var lowerHz = UiFrequenciesHz[i];
            var upperHz = UiFrequenciesHz[i + 1];
            if (centerHz > upperHz)
                continue;

            var t = (Math.Log(centerHz) - Math.Log(lowerHz)) / (Math.Log(upperHz) - Math.Log(lowerHz));
            return _bands[i] + (t * (_bands[i + 1] - _bands[i]));
        }

        return 0;
    }

    private void ReleaseEqualizer()
    {
        if (_equalizer is null)
            return;

        try
        {
            _equalizer.SetEnabled(false);
            _equalizer.Release();
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Failed to release equalizer: {ex.Message}");
        }
        finally
        {
            _equalizer = null;
        }
    }
}
