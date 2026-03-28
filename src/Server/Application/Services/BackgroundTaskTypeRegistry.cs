using System.Reflection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;

public sealed class BackgroundTaskTypeRegistry
{
    private readonly Dictionary<string, Type> _allowedTypes;

    public BackgroundTaskTypeRegistry(ILogger<BackgroundTaskTypeRegistry> logger)
    {
        _allowedTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IBaseRequest).IsAssignableFrom(t))
            .ToDictionary(t => t.FullName!, t => t);

        logger.LogInformation("BackgroundTaskTypeRegistry initialized with {Count} allowed request types", _allowedTypes.Count);
    }

    public Type? Resolve(string typeName)
    {
        _allowedTypes.TryGetValue(typeName, out var type);
        return type;
    }
}
