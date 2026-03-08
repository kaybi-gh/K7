using FFMpegCore;
using FFMpegCore.Enums;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Infrastructure.MediaProcessing;

public class ImageProcessor : IImageProcessor
{
    public async Task ConvertToWebPAsync(string inputPath, string outputPath, int quality, CancellationToken cancellationToken)
    {
        var outputFile = new FileInfo(outputPath);
        outputFile.Directory?.Create();

        await FFMpegArguments
            .FromFileInput(inputPath, verifyExists: false)
            .OutputToFile(outputPath, overwrite: true, options => options
                .WithCustomArgument($"-q:v {quality}"))
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously(throwOnError: true)
            .ConfigureAwait(false);
    }

    public async Task ResizeAsync(string inputPath, string outputPath, int maxWidth, int quality, CancellationToken cancellationToken)
    {
        var outputFile = new FileInfo(outputPath);
        outputFile.Directory?.Create();

        await FFMpegArguments
            .FromFileInput(inputPath, verifyExists: false)
            .OutputToFile(outputPath, overwrite: true, options => options
                .WithCustomArgument($"-vf \"scale={maxWidth}:-1\"")
                .WithCustomArgument($"-q:v {quality}"))
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously(throwOnError: true)
            .ConfigureAwait(false);
    }
}
