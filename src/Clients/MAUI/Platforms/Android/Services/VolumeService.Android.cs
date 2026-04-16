using Android.App;
using Android.Content;
using Android.Media;
using Stream = Android.Media.Stream;

namespace K7.Clients.MAUI.Services;

public partial class VolumeService
{
    public partial bool SupportsNativeVolume => true;

    public partial double Volume
    {
        get
        {
            var audioManager = GetAudioManager();
            if (audioManager is null) return 1.0;
            var max = audioManager.GetStreamMaxVolume(Stream.Music);
            return max > 0 ? (double)audioManager.GetStreamVolume(Stream.Music) / max : 1.0;
        }
    }

    public partial void SetVolume(double volume)
    {
        var audioManager = GetAudioManager();
        if (audioManager is null) return;
        var max = audioManager.GetStreamMaxVolume(Stream.Music);
        var target = (int)Math.Round(Math.Clamp(volume, 0, 1) * max);
        audioManager.SetStreamVolume(Stream.Music, target, 0);
    }

    private static AudioManager? GetAudioManager()
    {
        return Platform.CurrentActivity?.GetSystemService(Context.AudioService) as AudioManager;
    }
}
