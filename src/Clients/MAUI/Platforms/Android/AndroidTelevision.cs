using Android.Content;
using Android.Content.PM;
using Android.OS;
using K7.Clients.Shared.Helpers;
using UiMode = Android.Content.Res.UiMode;

namespace K7.Clients.MAUI.Platforms.Android;

internal static class AndroidTelevision
{
    public static bool IsDeviceTelevision(Context? context = null)
    {
        context ??= global::Android.App.Application.Context;
        var uiMode = context?.Resources?.Configuration?.UiMode ?? 0;
        var uiTv = (uiMode & UiMode.TypeMask) == UiMode.TypeTelevision;
        var packageManager = context?.PackageManager;
        var leanback = packageManager?.HasSystemFeature(PackageManager.FeatureLeanback) == true;
        var fireTv = packageManager?.HasSystemFeature(TelevisionLayout.FireTvFeature) == true;

        return TelevisionLayout.MatchesAndroidTelevision(
            uiTv,
            leanback,
            fireTv,
            Build.Model);
    }
}
