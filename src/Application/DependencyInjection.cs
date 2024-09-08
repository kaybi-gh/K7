using System.Reflection;
using MediaServer.Application.Common.Behaviours;
using MediaServer.Application.Services;
using MediaServer.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace MediaServer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddAutoMapper(Assembly.GetExecutingAssembly());
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));
        });

        services.AddHostedService<BackgroundTasksProcessingService>();
        services.AddScoped<IFileIndexerService, FileIndexerService>();
        services.AddScoped<IMovieMetadataProvider, TMDbMetadataProvider>(); // TODO - Make it customizable
        services.AddScoped<TMDbMetadataProvider>(); // TODO - Make it customizable

        return services;
    }
}
