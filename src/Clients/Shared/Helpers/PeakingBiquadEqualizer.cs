namespace K7.Clients.Shared.Helpers;

/// <summary>
/// 10-band peaking EQ matching the Web Audio graph (Q=1.4, same center frequencies).
/// Processes interleaved float32 PCM in-place.
/// </summary>
public sealed class PeakingBiquadEqualizer
{
    public static readonly double[] FrequenciesHz =
        [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    private const double Q = 1.4;
    private readonly object _gate = new();
    private readonly Biquad[] _filters = new Biquad[FrequenciesHz.Length];
    private double[] _gainsDb = new double[FrequenciesHz.Length];
    private bool _enabled;
    private int _sampleRate = 44100;
    private int _channels = 2;

    public PeakingBiquadEqualizer()
    {
        for (var i = 0; i < _filters.Length; i++)
            _filters[i] = new Biquad();
        RebuildLocked();
    }

    public void UpdateSettings(bool enabled, double[] gainsDb)
    {
        lock (_gate)
        {
            _enabled = enabled;
            if (gainsDb.Length >= _gainsDb.Length)
                _gainsDb = (double[])gainsDb.Clone();
            RebuildLocked();
        }
    }

    public void ConfigureFormat(int sampleRate, int channels)
    {
        if (sampleRate <= 0 || channels <= 0)
            return;

        lock (_gate)
        {
            if (_sampleRate == sampleRate && _channels == channels)
                return;

            _sampleRate = sampleRate;
            _channels = channels;
            RebuildLocked();
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            foreach (var filter in _filters)
                filter.Reset();
        }
    }

    public void ProcessInPlace(Span<float> interleavedSamples)
    {
        lock (_gate)
        {
            if (!_enabled || interleavedSamples.IsEmpty)
                return;

            var channels = Math.Max(1, _channels);
            for (var i = 0; i < _filters.Length; i++)
            {
                if (Math.Abs(_gainsDb[i]) < 0.01)
                    continue;

                _filters[i].ProcessInterleaved(interleavedSamples, channels);
            }
        }
    }

    private void RebuildLocked()
    {
        for (var i = 0; i < _filters.Length; i++)
        {
            _filters[i].DesignPeaking(_sampleRate, FrequenciesHz[i], Q, _enabled ? _gainsDb[i] : 0);
            _filters[i].Reset();
        }
    }

    private sealed class Biquad
    {
        private double _b0 = 1, _b1, _b2, _a1, _a2;
        private double[] _z1 = new double[8];
        private double[] _z2 = new double[8];

        public void DesignPeaking(int sampleRate, double frequencyHz, double q, double gainDb)
        {
            if (sampleRate <= 0 || frequencyHz <= 0 || q <= 0)
            {
                _b0 = 1;
                _b1 = _b2 = _a1 = _a2 = 0;
                return;
            }

            // Web Audio peaking EQ coefficients
            var a = Math.Pow(10.0, gainDb / 40.0);
            var w0 = 2.0 * Math.PI * frequencyHz / sampleRate;
            var alpha = Math.Sin(w0) / (2.0 * q);
            var cosW0 = Math.Cos(w0);

            var b0 = 1.0 + (alpha * a);
            var b1 = -2.0 * cosW0;
            var b2 = 1.0 - (alpha * a);
            var a0 = 1.0 + (alpha / a);
            var a1 = -2.0 * cosW0;
            var a2 = 1.0 - (alpha / a);

            _b0 = b0 / a0;
            _b1 = b1 / a0;
            _b2 = b2 / a0;
            _a1 = a1 / a0;
            _a2 = a2 / a0;
        }

        public void Reset()
        {
            Array.Clear(_z1);
            Array.Clear(_z2);
        }

        public void ProcessInterleaved(Span<float> samples, int channels)
        {
            if (channels > _z1.Length)
            {
                _z1 = new double[channels];
                _z2 = new double[channels];
            }

            var frameCount = samples.Length / channels;
            for (var frame = 0; frame < frameCount; frame++)
            {
                var baseIndex = frame * channels;
                for (var ch = 0; ch < channels; ch++)
                {
                    var x = samples[baseIndex + ch];
                    var y = (_b0 * x) + _z1[ch];
                    _z1[ch] = (_b1 * x) - (_a1 * y) + _z2[ch];
                    _z2[ch] = (_b2 * x) - (_a2 * y);
                    samples[baseIndex + ch] = (float)y;
                }
            }
        }
    }
}
