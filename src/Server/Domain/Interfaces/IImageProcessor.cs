namespace K7.Server.Domain.Interfaces;

/// <summary>
/// Handles image conversion and resizing operations.
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Converts an image to WebP format.
    /// </summary>
    Task ConvertToWebPAsync(string inputPath, string outputPath, int quality = 85, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resizes an image to fit within the specified width, maintaining aspect ratio, and saves as WebP.
    /// </summary>
    Task ResizeAsync(string inputPath, string outputPath, int maxWidth, int quality = 80, CancellationToken cancellationToken = default);
}
