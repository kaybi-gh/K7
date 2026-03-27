using K7.Server.Application.Helpers;
using K7.Server.Application.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace K7.Server.Infrastructure.FileSystem;

public static class FileSystemInitializerExtensions
{
    public static IServiceCollection EnsurePathsExist(this IServiceCollection services)
    {
        var paths = services.BuildServiceProvider().GetRequiredService<IOptions<PathsConfiguration>>().Value;

        var requiredPaths = new Dictionary<string, string>
        {
            ["Config"] = paths.Config,
            ["Logs"] = paths.Logs,
            ["Metadatas"] = paths.Metadatas,
            ["Transcoding"] = paths.Transcoding,
            ["Config/openiddict-keys"] = Path.Combine(paths.Config, "openiddict-keys")
        };

        var errors = new List<string>();
        foreach (var (name, path) in requiredPaths)
        {
            if (!PathAccessibilityHelper.IsDirectoryAccessible(path, out var error))
                errors.Add(error ?? $"{name} ('{path}'): unknown error");
        }

        if (errors.Count > 0)
            throw new UnauthorizedAccessException(
                $"The following paths are not accessible:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");

        return services;
    }
}
