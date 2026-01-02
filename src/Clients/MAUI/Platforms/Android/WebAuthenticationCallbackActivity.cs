using Android.App;
using Android.Content;
using Android.Content.PM;

namespace K7.Clients.MAUI.Platforms.Android;

[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView],
              Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
              DataScheme = K7_SCHEME,
              DataHost = LOGIN_CALLBACK)]
public class WebAuthenticationCallbackActivity : WebAuthenticatorCallbackActivity
{
    const string K7_SCHEME = "k7";
    const string LOGIN_CALLBACK = "login-callback";
}
