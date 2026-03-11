using System.Globalization;
using System.Text.RegularExpressions;
using FFMpegCore;
using FFMpegCore.Enums;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Infrastructure.MediaProcessing;

public partial class ImageProcessor : IImageProcessor
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

    [GeneratedRegex(@"(\d+)\s+(\d+)\s+(\d+)")]
    private static partial Regex RgbLineRegex();

    public async Task<string?> ExtractDominantColorAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        // Scale to 8x8, average to 1x1, output raw RGB text
        var result = await FFMpegArguments
            .FromFileInput(inputPath, verifyExists: false)
            .OutputToFile("-", overwrite: true, options => options
                .WithCustomArgument("-vf \"scale=8:8,avgblur=8\"")
                .WithCustomArgument("-frames:v 1")
                .WithCustomArgument("-pix_fmt rgb24")
                .ForceFormat("rawvideo"))
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously(throwOnError: false)
            .ConfigureAwait(false);

        // Fallback: use showinfo filter to get average color
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
            try
            {
                await FFMpegArguments
                    .FromFileInput(inputPath, verifyExists: false)
                    .OutputToFile(tempPath, overwrite: true, options => options
                        .WithCustomArgument("-vf \"scale=1:1\"")
                        .WithCustomArgument("-frames:v 1")
                        .WithCustomArgument("-pix_fmt rgb24")
                        .ForceFormat("rawvideo"))
                    .CancellableThrough(cancellationToken)
                    .ProcessAsynchronously(throwOnError: true)
                    .ConfigureAwait(false);

                var bytes = await File.ReadAllBytesAsync(tempPath, cancellationToken);
                if (bytes.Length >= 3)
                {
                    return $"{bytes[0]},{bytes[1]},{bytes[2]}";
                }
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
        catch
        {
            // Color extraction is best-effort
        }

        return null;
    }
}
