using K7.Server.Application.Helpers;
using K7.Server.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
}
