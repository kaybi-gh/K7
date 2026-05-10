using System.Reflection;
using K7.Server.Application.Common.Behaviours;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpClient(string.Empty, client =>
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"K7/{version}");
        });
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));
        });

        services.AddSingleton<BackgroundTaskQueue>();
        services.AddSingleton<IBackgroundTaskQueue>(sp => sp.GetRequiredService<BackgroundTaskQueue>());
        services.AddSingleton<BackgroundTaskTypeRegistry>();
        services.AddSingleton<BackgroundTasksProcessingService>();
        services.AddHostedService(sp => sp.GetRequiredService<BackgroundTasksProcessingService>());
        services.AddHostedService<MetadataRefreshSchedulerService>();
        services.AddSingleton<ActiveStreamTracker>();
        services.AddSingleton<IActiveStreamTracker>(sp => sp.GetRequiredService<ActiveStreamTracker>());
        services.AddSingleton<MediaQueryCacheInvalidator>();
        services.AddSingleton<IMediaQueryCacheInvalidator>(sp => sp.GetRequiredService<MediaQueryCacheInvalidator>());
        services.AddScoped<IFileIndexer, FileIndexer>();
        services.AddScoped<IMediaAccessGuard, MediaAccessGuard>();

        return services;
    }
}
