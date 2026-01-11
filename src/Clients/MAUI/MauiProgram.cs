using K7.Clients.MAUI.Services;
using K7.Clients.MAUI.Services.Authentication;
using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using CommunityToolkit.Maui;
using K7.Clients.MAUI.Interfaces;
using K7.Shared.Interfaces;
using K7.Shared.Services;
using Microsoft.AspNetCore.Components.WebView.Maui;

#if WINDOWS
using K7.Clients.MAUI.Platforms.Windows;
#endif

namespace K7.Clients.MAUI;

public static partial class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkitMediaElement()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

//        builder.ConfigureMauiHandlers(handlers =>
//        {
//#if WINDOWS
//    handlers.AddHandler<BlazorWebView, TransparentBlazorWebViewHandler>();
//#endif
//        });

        //#if WINDOWS
        //        builder.ConfigureWindowsLifecycleEvents();
        //#endif

        builder.Services.AddMudServices();
        builder.Services.ConfigurePlatformServices();

        builder.Services.AddSingleton<SidebarService>();
        builder.Services.AddSingleton<ThemeService>();

        builder.Services.AddHttpClient(nameof(K7ServerService));
        builder.Services.AddSingleton<IK7ServerService, K7ServerService>();
        builder.Services.AddSingleton<K7ServerManagerService>();

        builder.Services.AddSingleton<IDeviceService, DeviceService>();
        builder.Services.AddSingleton<IPlayerService, PlayerService>();
        builder.Services.AddSingleton<IMediaStreamSession, MediaSessionService>();
        builder.Services.AddSingleton<IDeviceStorageService, DeviceStorageService>();

        builder.Services.AddAuthorizationCore();
        builder.Services.AddSingleton<IMsalClientService, MsalClientService>();
        builder.Services.AddSingleton<ICustomAuthenticationStateProvider, CustomAuthenticationStateProvider>();
        builder.Services.AddSingleton(sp => (AuthenticationStateProvider)sp.GetRequiredService<ICustomAuthenticationStateProvider>());

        var app = builder.Build();

        var k7ServerManagerService = app.Services.GetRequiredService<K7ServerManagerService>();
        k7ServerManagerService.BaseAddressUpdated += (sender, baseAddress) => K7ServerManagerService_BaseAddressUpdated(sender, baseAddress, app.Services);

        return app;
    }

    private static async void K7ServerManagerService_BaseAddressUpdated(object? sender, string baseAddress, IServiceProvider services)
    {
        await DeviceInitializer.InitializeDeviceAsync(services);
    }

    static partial void ConfigurePlatformServices(this IServiceCollection services);
}
