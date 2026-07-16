using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Tests.Helpers.Smoke;

public static class MediatRHandlerResolution
{
    public static IReadOnlyList<Type> GetHandlerTypes(Assembly applicationAssembly)
    {
        return applicationAssembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
            .Where(t => t.GetInterfaces().Any(IsMediatRHandlerInterface))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();
    }

    public static void ResolveAllHandlers(IServiceProvider root, Assembly applicationAssembly)
    {
        using var scope = root.CreateScope();
        var failures = new List<string>();

        foreach (var handlerType in GetHandlerTypes(applicationAssembly))
        {
            try
            {
                _ = scope.ServiceProvider.GetRequiredService(handlerType);
            }
            catch (Exception ex)
            {
                failures.Add($"{handlerType.FullName}: {ex.Message}");
            }
        }

        if (failures.Count > 0)
            throw new InvalidOperationException(
                $"Failed to resolve MediatR handlers:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    private static bool IsMediatRHandlerInterface(Type interfaceType)
    {
        if (!interfaceType.IsGenericType)
            return false;

        var definition = interfaceType.GetGenericTypeDefinition();
        return definition == typeof(IRequestHandler<>) ||
               definition == typeof(IRequestHandler<,>) ||
               definition == typeof(INotificationHandler<>);
    }
}
