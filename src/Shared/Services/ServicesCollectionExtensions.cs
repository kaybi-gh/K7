using MediaClient.Shared.Domain.Interfaces;
using MediaClient.Shared.Services.MediaServer;
using Microsoft.Extensions.DependencyInjection;

namespace MediaClient.Shared.Services;

public static class ServicesCollectionExtensions
{
    public static void AddServerServices(this IServiceCollection services)
    {
        services.AddHttpClient<IMediaServerService, MediaServerService>("test2", client =>
        {
            client.BaseAddress = new Uri("https://localhost:5001");
        });
        services.AddScoped<SidebarService>();
        services.AddScoped<ThemeService>();
    }

    public static void AddClientServices(this IServiceCollection services)
    {
        services.AddHttpClient<IMediaServerService, MediaServerService>("test1", client =>
        {
            client.BaseAddress = new Uri("https://localhost:5001");
        });
        services.AddSingleton<SidebarService>();
        services.AddSingleton<ThemeService>();
    }
}
