using AudioToolbox;
using AVFoundation;
using CoreMedia;
using Foundation;
using K7.Clients.Shared.Helpers;
using MediaToolbox;

namespace K7.Clients.MAUI.Platforms.iOS.Services;

/// <summary>
/// Applies the shared 10-band peaking EQ to AVPlayer items via MTAudioProcessingTap.
/// </summary>
internal sealed class IosAudioEqualizer : IDisposable
{
    private readonly PeakingBiquadEqualizer _processor = new();
    private MTAudioProcessingTap? _tap;
    private bool _disposed;

    public IosAudioEqualizer()
    {
        CreateTap();
    }

    public void UpdateSettings(bool enabled, double[] bands)
        => _processor.UpdateSettings(enabled, bands);

    public void AttachToPlayerItem(AVPlayerItem? item)
    {
        if (_disposed || item is null || _tap is null)
            return;

        item.Asset.LoadValuesAsynchronously(["tracks"], () =>
        {
            if (_disposed || _tap is null)
                return;

            MainThread.BeginInvokeOnMainThread(() => TryAttach(item));
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _tap?.Dispose();
        _tap = null;
    }

    private void TryAttach(AVPlayerItem item)
    {
        try
        {
            var status = item.Asset.StatusOfValue("tracks", out var error);
            if (status != AVKeyValueStatus.Loaded || error is not null)
                return;

            var mediaType = AVMediaTypes.Audio.GetConstant();
            if (mediaType is null)
                return;

            var audioTrack = item.Asset.TracksWithMediaType(mediaType).FirstOrDefault();
            if (audioTrack is null || _tap is null)
                return;

            var inputParams = AVMutableAudioMixInputParameters.FromTrack(audioTrack);
            if (inputParams is null)
                return;

            inputParams.AudioTapProcessor = _tap;
            item.AudioMix = new AVMutableAudioMix
            {
                InputParameters = [inputParams]
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[K7-iOS-EQ] Attach failed: {ex.Message}");
        }
    }

    private void CreateTap()
    {
        try
        {
            var callbacks = new MTAudioProcessingTapCallbacks(OnProcess)
            {
                Prepare = OnPrepare,
                Unprepare = OnUnprepare
            };

            _tap = new MTAudioProcessingTap(callbacks, MTAudioProcessingTapCreationFlags.PostEffects);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[K7-iOS-EQ] Tap create exception: {ex.Message}");
            _tap = null;
        }
    }

    private void OnPrepare(MTAudioProcessingTap tap, nint maxFrames, ref AudioStreamBasicDescription processingFormat)
    {
        var channels = Math.Max(1, (int)processingFormat.ChannelsPerFrame);
        var sampleRate = processingFormat.SampleRate > 0 ? (int)processingFormat.SampleRate : 44100;
        _processor.ConfigureFormat(sampleRate, channels);
        _processor.Reset();
    }

    private void OnUnprepare(MTAudioProcessingTap tap)
        => _processor.Reset();

    private unsafe void OnProcess(
        MTAudioProcessingTap tap,
        nint numberFrames,
        MTAudioProcessingTapFlags flags,
        AudioBuffers bufferList,
        out nint numberFramesOut,
        out MTAudioProcessingTapFlags flagsOut)
    {
        numberFramesOut = numberFrames;
        flagsOut = flags;

        try
        {
            var status = tap.GetSourceAudio(
                numberFrames,
                bufferList,
                out flagsOut,
                out _,
                out numberFramesOut);

            if (status != MTAudioProcessingTapError.None || bufferList.Count <= 0)
                return;

            var buffer = bufferList[0];
            if (buffer.Data == IntPtr.Zero || buffer.DataByteSize <= 0)
                return;

            var sampleCount = (int)buffer.DataByteSize / sizeof(float);
            if (sampleCount <= 0)
                return;

            var span = new Span<float>((void*)buffer.Data, sampleCount);
            _processor.ProcessInPlace(span);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[K7-iOS-EQ] Process failed: {ex.Message}");
        }
    }
}
