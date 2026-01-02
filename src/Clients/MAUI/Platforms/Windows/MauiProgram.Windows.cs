using K7.Clients.MAUI.Interfaces;
using K7.Clients.MAUI.Platforms.Windows.Services;

namespace K7.Clients.MAUI;
public static partial class MauiProgram
{
    static partial void ConfigurePlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<ICodecService, CodecService>();
        services.AddSingleton<IDeviceIdService, DeviceIdService>();
    }
}
