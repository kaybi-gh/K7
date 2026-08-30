using K7.Clients.MAUI.Interfaces;
using K7.Clients.Shared.Helpers;
using Windows.Graphics.Display;

namespace K7.Clients.MAUI.Platforms.Windows.Services;

/// <summary>
/// Reports LibVLC Direct Play capabilities (D3D11VA when the GPU has the decoder).
/// Not WebView2 MSE and not Media Foundation HLS.
/// </summary>
public class CodecService : ICodecService
{
    public Task<bool> GetHdrSupportAsync()
    {
        try
        {
            if (MainThread.IsMainThread)
                return Task.FromResult(ReadHdrSupport());

            return MainThread.InvokeOnMainThreadAsync(ReadHdrSupport);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static bool ReadHdrSupport()
    {
        var info = DisplayInformation.GetForCurrentView();
        var advanced = info.GetAdvancedColorInfo();
        return advanced.CurrentAdvancedColorKind != AdvancedColorKind.StandardDynamicRange;
    }

    public Task<string[]> GetSupportedVideoCodecsAsync() =>
        Task.FromResult(LibVlcWindowsCapabilities.VideoCodecs);

    public Task<string[]> GetSupportedAudioCodecsAsync() =>
        Task.FromResult(LibVlcWindowsCapabilities.AudioCodecs);

    public Task<string[]> GetSupportedContainersAsync() =>
        Task.FromResult(LibVlcWindowsCapabilities.GetContainers());
}
