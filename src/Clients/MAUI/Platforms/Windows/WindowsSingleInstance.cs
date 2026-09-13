using System.Diagnostics;
using K7.Clients.MAUI.Services.Authentication;
using Microsoft.Windows.AppLifecycle;

namespace K7.Clients.MAUI.Platforms.Windows;

/// <summary>
/// Registers a WinUI instance key so a <c>k7://</c> launch is redirected to the
/// process already waiting on Sign in, instead of booting a second MAUI host.
/// </summary>
internal static class WindowsSingleInstance
{
    internal const string InstanceKey = "K7";

    public static bool RedirectIfSecondary()
    {
        try
        {
            var instance = AppInstance.FindOrRegisterForKey(InstanceKey);
            if (instance.IsCurrent)
                return false;

            NativeAuthTrace.Write("protocol-redirect");
            var args = AppInstance.GetCurrent().GetActivatedEventArgs();
            instance.RedirectActivationToAsync(args).AsTask().GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("K7 MAUI - single instance redirect failed: " + ex);
            NativeAuthTrace.Write("protocol-error", "redirect");
            return false;
        }
    }
}
