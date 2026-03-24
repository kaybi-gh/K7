using K7.Server.Application.Helpers;
using K7.Server.Infrastructure.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Infrastructure.FileSystem;

public static class FileSystemInitializerExtensions
{
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
        var pathsAreAccessible = PathAccessibilityHelper.IsDirectoryAccessible(_pathsConfiguration.Config)
        && PathAccessibilityHelper.IsDirectoryAccessible(_pathsConfiguration.Logs)
        && PathAccessibilityHelper.IsDirectoryAccessible(_pathsConfiguration.Metadatas)
        && PathAccessibilityHelper.IsDirectoryAccessible(_pathsConfiguration.Transcoding);

        if (!pathsAreAccessible)
        {
            _logger.LogError("An error occurred while initializing the file system.");
            throw new UnauthorizedAccessException();
        }

        PathAccessibilityHelper.IsDirectoryAccessible(Path.Combine(_pathsConfiguration.Config, "openiddict-keys"));
    }
}
