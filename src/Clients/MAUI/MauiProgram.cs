using K7.Clients.MAUI.Services;
using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Services;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace K7.Clients.MAUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

        builder.Services.AddMudServices();
        builder.Services.AddConfigurations(builder.Configuration);
        builder.Services.AddClientServices();
        builder.Services.AddSingleton<IFormFactorService, FormFactorService>();
        return builder.Build();
    }
}
