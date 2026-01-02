using Android.Content;
using Android.Hardware.Display;
using Android.Views;
using K7.Clients.MAUI.Interfaces;

namespace K7.Clients.MAUI.Platforms.Android.Services;

public class CodecService : ICodecService
{
    public Task<bool> GetHdrSupportAsync()
    {
        var displayManager = (DisplayManager?)global::Android.App.Application.Context.GetSystemService(Context.DisplayService);
        var display = displayManager?.GetDisplay(Display.DefaultDisplay);

        if (display == null)
        {
            return Task.FromResult(false);
        }

#if ANDROID33_0_OR_GREATER
        return Task.FromResult(display.IsHdr);
#elif ANDROID26_0_OR_GREATER
        var hdrCapabilities = display.GetHdrCapabilities();
        return Task.FromResult(hdrCapabilities?.GetSupportedHdrTypes()?.Length > 0);
#else
    return Task.FromResult(false);
#endif
    }

}
