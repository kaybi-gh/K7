using K7.Clients.MAUI.Constants;
using K7.Shared.Interfaces;

namespace K7.Clients.MAUI.Services;
public class K7ServerManagerService
{
    public event EventHandler<string>? BaseAddressUpdated;
    private readonly IK7ServerService _k7ServerService;

    public K7ServerManagerService(IK7ServerService k7ServerService)
    {
        _k7ServerService = k7ServerService;
    }

    public void UpdateBaseAddress(string baseAddress)
    {
        var newUri = new Uri(baseAddress);
        if (_k7ServerService.HttpClient.BaseAddress?.OriginalString == newUri.OriginalString)
            return;

        _k7ServerService.HttpClient.BaseAddress = newUri;

        BaseAddressUpdated?.Invoke(this, baseAddress);
    }

    public async Task RemoveRegisteredBackendUrlAsync()
    {
        Preferences.Remove(PreferenceKeys.K7_SERVER_URL);
        Application.Current!.Quit(); // TODO - Move back to SetupPage
        
        await Task.Delay(0);
        //MainThread.BeginInvokeOnMainThread(() =>
        //{
        //    Application.Current!.Windows[0]!.Page = GetRightPageToDisplay();
        //});
    }
}
