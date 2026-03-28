// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace K7.Clients.MAUI.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        var appInstance = AppInstance.GetCurrent();
        var e = appInstance.GetActivatedEventArgs();

        if (e.Kind == ExtendedActivationKind.Protocol &&
            e.Data is ProtocolActivatedEventArgs protocolArgs)
        {
            var instances = AppInstance.GetInstances();
            Task.Run(async () =>
            {
                await Task.WhenAll(instances.Select(async q => await q.RedirectActivationToAsync(e)));
            });

            var window = (Microsoft.UI.Xaml.Window)App.Current.Application.Windows[0].Handler!.PlatformView!;
            window.Close();
            return;
        }

        // Si ce n'est pas une activation par protocole, écouter les activations futures
        appInstance.Activated += AppInstance_Activated;
    }

    //protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    //{
    //    //base.OnLaunched(args);

    //    //if (args.Arguments.StartsWith("k7://"))
    //    //{
    //    //    HandleDeepLink(args.Arguments);
    //    //}
    //}

    //private void HandleDeepLink(string uri)
    //{
    //    Debug.WriteLine($"[HandleDeepLink] URI reçue : {uri}");
    //    MainThread.BeginInvokeOnMainThread(() =>
    //    {
    //        // TODO: Traiter l'URI reçue et rediriger l'utilisateur
    //    });
    //}

    private static void AppInstance_Activated(object? sender, AppActivationArguments e)
    {
        if (e.Kind != ExtendedActivationKind.Protocol ||
            e.Data is not ProtocolActivatedEventArgs protocol)
        {
            var protocol2 = e.Data as ProtocolActivatedEventArgs;
            return;
        }

        if (protocol.Uri.Host == "login-callback")
        {
            /*MainThread.BeginInvokeOnMainThread(async () =>
            {
                var authService = MauiWinUIApplication.Current.Services.GetService<IAuthenticationService>();
                if (authService != null)
                {
                    await authService.LoginCallbackAsync(protocol.Uri);
                }
                else
                {
                    Debug.WriteLine("AuthenticationService introuvable !");
                }
            });*/
        }
    }
}
