using Android.App;
using Android.Content;
using Android.Content.PM;
using OpenIddict.Client.SystemIntegration;

namespace K7.Clients.MAUI.Platforms.Android;

[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView],
              Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
              DataScheme = "k7",
              DataHost = "callback",
              DataPath = "/login")]
public class CallbackActivity : Activity
{
    protected override async void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (Intent is not Intent intent)
        {
            Finish();
            return;
        }

        var provider = IPlatformApplication.Current?.Services ??
            throw new InvalidOperationException("The dependency injection container cannot be resolved.");
        var service = provider.GetRequiredService<OpenIddictClientSystemIntegrationService>();

        try
        {
            await service.HandleCustomTabsIntentAsync(intent);
        }
        finally
        {
            var mainIntent = new Intent(this, typeof(MainActivity));
            mainIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            StartActivity(mainIntent);
            Finish();
        }
    }
}
