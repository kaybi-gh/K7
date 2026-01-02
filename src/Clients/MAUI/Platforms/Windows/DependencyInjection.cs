using System.Diagnostics;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace K7.Clients.MAUI.Platforms.Windows;

public static class DependencyInjection
{
    /*public static void ConfigureWindowsLifecycleEvents(this MauiAppBuilder builder)
    {
        builder.ConfigureLifecycleEvents(lc =>
        {
            lc.AddWindows(static win =>
            {
                win.OnLaunched(static async (app, args) =>
                {
                    var appInstance = AppInstance.GetCurrent();
                    var e = appInstance.GetActivatedEventArgs();

                    if (e.Kind != ExtendedActivationKind.Protocol
                        || e.Data is not ProtocolActivatedEventArgs protocol)
                    {
                        appInstance.Activated += AppInstance_Activated;
                        return;
                    }

                    var instances = AppInstance.GetInstances();
                    await Task.WhenAll(instances.Select(async q => await q.RedirectActivationToAsync(e)));

                    app.Exit();
                });
            });
        });
    }*/

    //private static void AppInstance_Activated(object? sender, AppActivationArguments e)
    //{
    //    if (e.Kind != ExtendedActivationKind.Protocol ||
    //        e.Data is not ProtocolActivatedEventArgs protocol)
    //    {
    //        return;
    //    }

    //    // Process your activation here
    //    Debug.WriteLine("URI activated: " + protocol.Uri);
    //}

    

}
