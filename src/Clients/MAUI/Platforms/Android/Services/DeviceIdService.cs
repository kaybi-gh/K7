using AndroidApp = Android.App.Application;
using Android.Provider;
using K7.Clients.MAUI.Interfaces;

namespace K7.Clients.MAUI.Platforms.Android.Services;

public class DeviceIdService : IDeviceIdService
{
    public string? GetDeviceId()
    {
        return Settings.Secure.GetString(AndroidApp.Context.ContentResolver, Settings.Secure.AndroidId);
    }
}
