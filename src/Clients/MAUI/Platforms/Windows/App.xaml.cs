using K7.Clients.MAUI.Platforms.Windows;
using K7.Clients.MAUI.Services.Authentication;

namespace K7.Clients.MAUI.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        NativeAuthTrace.Install();

        var callback = WindowsProtocolActivation.TryGetStartupCallback();
        if (callback is not null)
            WindowsProtocolCallbackFile.Write(callback);

        if (WindowsSingleInstance.RedirectIfSecondary())
            Environment.Exit(0);

        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
