using MediaClient.Shared.Services;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace MediaClient.MAUI;

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
        return builder.Build();
    }
}
