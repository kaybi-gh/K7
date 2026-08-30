namespace K7.Clients.MAUI.Services;

/// <summary>
/// Windows video volume is software gain only (<c>IPlayerService</c> -> LibVLC
/// <c>MediaPlayer.Volume</c> or Video.js <c>HTMLMediaElement.volume</c>).
/// Do not drive the WASAPI "K7" session here: that double-attenuates Direct vs HLS.
/// </summary>
public partial class VolumeService
{
    private double _volume = 1;

    public partial bool SupportsNativeVolume => false;

    public partial double Volume => _volume;

    public partial void SetVolume(double volume) =>
        _volume = Math.Clamp(volume, 0, 1);
}
