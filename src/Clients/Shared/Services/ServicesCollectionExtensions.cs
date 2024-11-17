using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Services.MediaServer;
using K7.Clients.Shared.Services.MediaServer.Mappings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using AutoMapper;

namespace K7.Clients.Shared.Services;

public static class ServicesCollectionExtensions
{
    public static void AddServerServices(this IServiceCollection services)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly(), Assembly.GetAssembly(typeof(ActorDtoMapping)));
        var mediaServerConfiguration = services.BuildServiceProvider().GetRequiredService<IOptions<MediaServerConfiguration>>().Value;
        services.AddHttpClient<IMediaServerService, MediaServerService>(client =>
        {
            client.BaseAddress = new Uri(mediaServerConfiguration!.BaseUrl);
        });
        services.AddScoped<SidebarService>();
        services.AddScoped<ThemeService>();
        services.AddScoped<PlayerService>();
        services.AddScoped<IMemberValueResolver<object, object, Uri?, string?>, MediaServerAbsoluteUriResolver>();
    }

    public static void AddClientServices(this IServiceCollection services)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly(), Assembly.GetAssembly(typeof(ActorDtoMapping)));
        var mediaServerConfiguration = services.BuildServiceProvider().GetRequiredService<IOptions<MediaServerConfiguration>>().Value;
        services.AddHttpClient<IMediaServerService, MediaServerService>(client =>
        {
            client.BaseAddress = new Uri(mediaServerConfiguration!.BaseUrl);
        });
        services.AddSingleton<SidebarService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<PlayerService>();
        services.AddScoped<IMemberValueResolver<object, object, Uri?, string?>, MediaServerAbsoluteUriResolver>();
    }
}
