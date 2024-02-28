using Microsoft.Extensions.DependencyInjection;

namespace MediaClient.Shared.Services;

public static class ServicesCollectionExtensions
{
    public static void AddServerServices(this IServiceCollection services)
    {
        services.AddScoped<ICurrentDeviceService, CurrentDeviceService>();
        services.AddScoped<SidebarService>();
        services.AddScoped<ThemeService>();
    }

    public static void AddClientServices(this IServiceCollection services)
    {
        services.AddSingleton<ICurrentDeviceService, CurrentDeviceService>();
        services.AddSingleton<SidebarService>();
        services.AddSingleton<ThemeService>();
    }
}
