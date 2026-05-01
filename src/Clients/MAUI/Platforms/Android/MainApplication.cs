using Android.App;
using Android.Runtime;
using K7.Clients.Shared.Interfaces;

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
            TryReportError(args.Exception, "AndroidEnvironment.UnhandledExceptionRaiser");
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            System.Diagnostics.Debug.WriteLine($"[K7-CRASH] AppDomain.UnhandledException: {args.ExceptionObject}");
            if (args.ExceptionObject is Exception ex)
            {
                TryReportError(ex, "AppDomain.UnhandledException");
            }
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            System.Diagnostics.Debug.WriteLine($"[K7-CRASH] TaskScheduler.UnobservedTaskException: {args.Exception}");
            TryReportError(args.Exception, "TaskScheduler.UnobservedTaskException");
            args.SetObserved();
        };
    }

    private static void TryReportError(Exception exception, string context)
    {
        try
        {
            var reporter = IPlatformApplication.Current?.Services.GetService<IClientErrorReporter>();
            reporter?.ReportError(exception, context);
        }
        catch
        {
            // Best-effort
        }
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
