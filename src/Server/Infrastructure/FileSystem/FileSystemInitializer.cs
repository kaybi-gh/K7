using K7.Server.Application.Helpers;
using K7.Server.Infrastructure.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Infrastructure.FileSystem;

public static class FileSystemInitializerExtensions
{
    public static IServiceCollection EnsurePathsExist(this IServiceCollection services)
    {
        var paths = services.BuildServiceProvider().GetRequiredService<IOptions<PathsConfiguration>>().Value;

        var pathsAreAccessible = PathAccessibilityHelper.IsDirectoryAccessible(paths.Config)
            && PathAccessibilityHelper.IsDirectoryAccessible(paths.Logs)
            && PathAccessibilityHelper.IsDirectoryAccessible(paths.Metadatas)
            && PathAccessibilityHelper.IsDirectoryAccessible(paths.Transcoding)
            && PathAccessibilityHelper.IsDirectoryAccessible(Path.Combine(paths.Config, "openiddict-keys"));

        if (!pathsAreAccessible)
            throw new UnauthorizedAccessException("One or more configured paths are not accessible.");

        return services;
    }

    public static void InitializeFileSystem(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<ApplicationFileSystemInitializer>();
        initializer.Initialize();
    }
}

public class ApplicationFileSystemInitializer
{
    private readonly ILogger<ApplicationFileSystemInitializer> _logger;
    private readonly PathsConfiguration _pathsConfiguration;

    public ApplicationFileSystemInitializer(ILogger<ApplicationFileSystemInitializer> logger, IOptions<PathsConfiguration> pathsConfiguration)
    {
        _logger = logger;
        _pathsConfiguration = pathsConfiguration.Value;
    }

    public void Initialize()
    {
        _logger.LogInformation("File system paths validated.");
    }
}
