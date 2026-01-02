using System.Reflection;
using K7.Server.Web.Endpoints;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace K7.Server.Web.Infrastructure;

public static partial class WebApplicationExtensions
{
    public static IServiceCollection AddEndpoints(
        this IServiceCollection services)
    {
        var assembly = typeof(Program).Assembly;

        var serviceDescriptors = assembly
            .DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type));

        services.TryAddEnumerable(serviceDescriptors);

        return services;
    }

    public static WebApplication MapEndpoints(this WebApplication app)
    {
        var endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();

        foreach (var endpoint in endpoints)
        {
            endpoint.Map(app);
        }

        return app;
    }
}

