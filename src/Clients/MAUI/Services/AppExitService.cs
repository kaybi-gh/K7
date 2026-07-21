using K7.Clients.Shared.Interfaces;

namespace K7.Clients.MAUI.Services;

public sealed class AppExitService : IAppExitService
{
    public void Exit()
    {
#if ANDROID
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity is not null)
            {
                activity.FinishAffinity();
                return;
            }
        }
        catch
        {
            // Fall through to Quit.
        }
#endif
        Microsoft.Maui.Controls.Application.Current?.Quit();
    }
}
