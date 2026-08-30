using Microsoft.Maui.Controls;

namespace K7.Clients.MAUI.Platforms.Android;

/// <summary>
/// Full-screen XAML chrome over SurfaceView still composites on Amlogic when chrome
/// is hidden. Stop drawing that layer so the HDMI plane is not blended every vsync.
/// Text cues live on a sibling view so this does not hide SRT.
/// </summary>
internal static class AndroidOverlayComposition
{
    internal static void SetDraws(VisualElement element, bool draws)
    {
        try
        {
            if (element.Handler?.PlatformView is not global::Android.Views.View view)
                return;

            view.SetWillNotDraw(!draws);
            view.SetLayerType(global::Android.Views.LayerType.None, null);
            view.Visibility = draws
                ? global::Android.Views.ViewStates.Visible
                : global::Android.Views.ViewStates.Invisible;
        }
        catch
        {
        }
    }
}
