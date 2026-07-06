using FFMpegCore;
using FFMpegCore.Enums;
using K7.Server.Domain.Interfaces;
using SkiaSharp;
using Svg.Skia;

namespace K7.Server.Infrastructure.MediaProcessing;

public class ImageProcessor : IImageProcessor
{
    public bool IsSvgFile(string path) =>
        string.Equals(Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase);

    public async Task ConvertToWebPAsync(string inputPath, string outputPath, int quality, CancellationToken cancellationToken)
    {
        if (IsSvgFile(inputPath))
        {
            await RasterizeSvgToWebPAsync(inputPath, outputPath, int.MaxValue, quality, cancellationToken);
            return;
        }

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
        if (IsSvgFile(inputPath))
        {
            await RasterizeSvgToWebPAsync(inputPath, outputPath, maxWidth, quality, cancellationToken);
            return;
        }

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

    public Task RasterizeSvgToWebPAsync(
        string svgPath,
        string outputPath,
        int maxWidth,
        int quality,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outputFile = new FileInfo(outputPath);
            outputFile.Directory?.Create();

            using var svg = new SKSvg();
            if (svg.Load(svgPath) is null || svg.Picture is null)
                throw new InvalidOperationException($"Failed to load SVG image from {svgPath}");

            var bounds = svg.Picture.CullRect;
            var targetWidth = maxWidth == int.MaxValue
                ? (int)Math.Ceiling(bounds.Width > 0 ? bounds.Width : 1200)
                : maxWidth;
            var scale = bounds.Width > 0
                ? targetWidth / bounds.Width
                : 1f;
            var targetHeight = (int)Math.Max(1, Math.Round(bounds.Height > 0 ? bounds.Height * scale : targetWidth));

            var imageInfo = new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(imageInfo);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            if (bounds.Width > 0 && bounds.Height > 0)
                canvas.Scale(scale);
            else
                canvas.Scale((float)targetWidth / 1200f);

            canvas.DrawPicture(svg.Picture);
            canvas.Flush();

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Webp, quality);
            if (data is null)
                throw new InvalidOperationException($"Failed to encode SVG raster output to WebP for {svgPath}");

            using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            data.SaveTo(stream);
        }, cancellationToken);

    public async Task<string?> ExtractDominantColorAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        if (IsSvgFile(inputPath))
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var svg = new SKSvg();
                if (svg.Load(inputPath) is null || svg.Picture is null)
                    return null;

                const int sampleSize = 8;
                var imageInfo = new SKImageInfo(sampleSize, sampleSize, SKColorType.Rgba8888, SKAlphaType.Premul);
                using var surface = SKSurface.Create(imageInfo);
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);

                var bounds = svg.Picture.CullRect;
                var scale = bounds.Width > 0
                    ? sampleSize / bounds.Width
                    : 1f;
                canvas.Scale(scale);
                canvas.DrawPicture(svg.Picture);
                canvas.Flush();

                using var image = surface.Snapshot();
                using var bitmap = SKBitmap.FromImage(image);
                long red = 0;
                long green = 0;
                long blue = 0;
                var count = 0;

                for (var y = 0; y < sampleSize; y++)
                {
                    for (var x = 0; x < sampleSize; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        if (pixel.Alpha < 16)
                            continue;

                        red += pixel.Red;
                        green += pixel.Green;
                        blue += pixel.Blue;
                        count++;
                    }
                }

                if (count == 0)
                    return null;

                return $"{red / count},{green / count},{blue / count}";
            }, cancellationToken);
        }

        await FFMpegArguments
            .FromFileInput(inputPath, verifyExists: false)
            .OutputToFile("-", overwrite: true, options => options
                .WithCustomArgument("-vf \"scale=8:8,avgblur=8\"")
                .WithCustomArgument("-frames:v 1")
                .WithCustomArgument("-pix_fmt rgb24")
                .ForceFormat("rawvideo"))
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously(throwOnError: false)
            .ConfigureAwait(false);

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
