using Microsoft.Maui.Controls;

namespace K7.Clients.MAUI.Platforms.Android;

/// <summary>
/// Full-screen XAML chrome over SurfaceView still composites on Android TV when chrome
/// is hidden. MAUI remaps IsVisible=true to View.Visible on layout, which undoes a
/// one-shot Invisible/WillNotDraw. Park the layer off-screen and force GONE so the
/// HDMI plane is not blended every vsync. Text cues live on a sibling view so this
/// does not hide SRT.
/// </summary>
internal static class AndroidOverlayComposition
{
    private const float HiddenTranslationX = 4096f;

    internal static void SetDraws(VisualElement element, bool draws)
    {
        try
        {
            // MAUI keeps the overlay IsVisible for the whole session (input / skip / veil).
            // Translate it away so a mapper that restores View.Visible still cannot blend
            // a full-screen transparent Grid over SurfaceView.
            element.TranslationX = draws ? 0 : HiddenTranslationX;

            if (element.Handler?.PlatformView is not global::Android.Views.View view)
                return;

            view.SetWillNotDraw(!draws);
            view.SetLayerType(global::Android.Views.LayerType.None, null);
            view.SetBackgroundColor(global::Android.Graphics.Color.Transparent);
            view.Background = null;
            view.Elevation = 0;
            view.TranslationZ = 0;
            view.Visibility = draws
                ? global::Android.Views.ViewStates.Visible
                : global::Android.Views.ViewStates.Gone;
        }
        catch
        {
        }
    }

    /// <summary>
    /// Restore drawing before the overlay is hidden. Leaving the layer Invisible after
    /// HideAsync keeps a blocking surface over the restored Blazor WebView.
    /// </summary>
    internal static void Reset(VisualElement element) => SetDraws(element, draws: true);
}
