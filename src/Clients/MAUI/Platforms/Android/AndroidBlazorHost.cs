#if ANDROID
using Android.App;

namespace K7.Clients.MAUI.Platforms.Android;

/// <summary>
/// Picks the Blazor page that belongs to the visible activity.
/// A long TV standby destroys the activity while the process stays up (media foreground
/// service). If window teardown throws, the old window stays in
/// <see cref="Microsoft.Maui.Controls.Application.Windows"/> and the recreated activity
/// paints select-profile on top of it. Keys must not keep hitting that hidden page.
/// </summary>
internal static class AndroidBlazorHost
{
    public static BlazorPage? PageFor(Activity? activity)
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app is null || app.Windows.Count == 0)
            return null;

        BlazorPage? newest = null;
        for (var i = app.Windows.Count - 1; i >= 0; i--)
        {
            if (app.Windows[i].Page is not BlazorPage page)
                continue;

            newest ??= page;
            if (activity is not null && ReferenceEquals(app.Windows[i].Handler?.PlatformView, activity))
                return page;
        }

        return newest;
    }

    /// <summary>
    /// Drop windows left behind by an aborted destroy. Only runs once this activity
    /// actually hosts a window, so a handler that is not attached yet is left alone.
    /// </summary>
    public static void CloseStaleWindows(Activity current)
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app is null || app.Windows.Count <= 1)
            return;

        var hostedHere = false;
        for (var i = 0; i < app.Windows.Count; i++)
        {
            if (ReferenceEquals(app.Windows[i].Handler?.PlatformView, current))
            {
                hostedHere = true;
                break;
            }
        }

        if (!hostedHere)
            return;

        foreach (var window in app.Windows.ToArray())
        {
            if (ReferenceEquals(window.Handler?.PlatformView, current))
                continue;

            if (window.Handler?.PlatformView is Activity host && !host.IsDestroyed)
                continue;

            try
            {
                app.CloseWindow(window);
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Warn("K7-Lifecycle", "Close stale window failed: " + ex.Message);
            }
        }
    }
}
#endif
