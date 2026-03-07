using Android.App;
using Android.Runtime;

namespace K7.Clients.MAUI;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
        AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
        {
            System.Diagnostics.Debug.WriteLine($"[K7-CRASH] AndroidEnvironment.UnhandledExceptionRaiser: {args.Exception}");
            args.Handled = false;
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            System.Diagnostics.Debug.WriteLine($"[K7-CRASH] AppDomain.UnhandledException: {args.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            System.Diagnostics.Debug.WriteLine($"[K7-CRASH] TaskScheduler.UnobservedTaskException: {args.Exception}");
        };
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
