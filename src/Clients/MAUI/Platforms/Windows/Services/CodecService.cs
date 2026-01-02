using K7.Clients.MAUI.Interfaces;
using Microsoft.UI.Dispatching;
using Windows.Graphics.Display;

namespace K7.Clients.MAUI.Platforms.Windows.Services;

public class CodecService : ICodecService
{
    public async Task<bool> GetHdrSupportAsync()
    {
        bool supportsHdr = false;

        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        if (dispatcherQueue != null)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();

            dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    var displayInfo = DisplayInformation.GetForCurrentView();
                    supportsHdr = displayInfo.GetAdvancedColorInfo().CurrentAdvancedColorKind != AdvancedColorKind.StandardDynamicRange;
                }
                catch
                {
                    supportsHdr = false;
                }
                taskCompletionSource.SetResult(supportsHdr);
            });

            return await taskCompletionSource.Task;
        }

        return false;
    }
}
