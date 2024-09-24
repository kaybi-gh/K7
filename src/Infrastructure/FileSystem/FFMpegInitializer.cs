using FFMpegCore;
using FFMpegCore.Helpers;
using MediaServer.Infrastructure.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaServer.Infrastructure.FileSystem;

public static class FFMpegInitializerExtensions
{
    public static void InitializeFFMpeg(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<FFMpegInitializer>();
        initializer.Initialize();
    }
}

public class FFMpegInitializer
{
    private readonly ILogger<ApplicationFileSystemInitializer> _logger;
    private readonly PathsConfiguration _pathsConfiguration;

    public FFMpegInitializer(ILogger<ApplicationFileSystemInitializer> logger,
        IOptions<PathsConfiguration> pathsConfiguration)
    {
        _logger = logger;
        _pathsConfiguration = pathsConfiguration.Value;
    }

    public void Initialize()
    {
        GlobalFFOptions.Configure(new FFOptions()
        {
            BinaryFolder = _pathsConfiguration.FFMpegBinaryFolder ?? ""
        });
        FFMpegHelper.VerifyFFMpegExists(GlobalFFOptions.Current);
    }
}
