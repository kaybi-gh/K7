using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Helpers;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.MetadataPictures.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;

public record GenerateMetadataPictureVariantsCommand : IRequest
{
    public required Guid MetadataPictureId { get; init; }
}

public class GenerateMetadataPictureVariantsCommandHandler : IRequestHandler<GenerateMetadataPictureVariantsCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IImageProcessor _imageProcessor;
    private readonly PathsConfiguration _pathsConfiguration;
    private readonly ILogger<GenerateMetadataPictureVariantsCommandHandler> _logger;
    private readonly MediaPictureReadyNotifier _pictureReadyNotifier;
    private readonly IBackgroundTaskExecutionContext _taskExecutionContext;

    public GenerateMetadataPictureVariantsCommandHandler(
        IApplicationDbContext context,
        IImageProcessor imageProcessor,
        IOptions<PathsConfiguration> pathsConfiguration,
        ILogger<GenerateMetadataPictureVariantsCommandHandler> logger,
        MediaPictureReadyNotifier pictureReadyNotifier,
        IBackgroundTaskExecutionContext taskExecutionContext)
    {
        _context = context;
        _imageProcessor = imageProcessor;
        _pathsConfiguration = pathsConfiguration.Value;
        _logger = logger;
        _pictureReadyNotifier = pictureReadyNotifier;
        _taskExecutionContext = taskExecutionContext;
    }

    private const string LocalPictureVariantsFolder = "LocalPictures";

    private bool IsUnderMetadataRoot(string path)
    {
        // No metadata root configured: nowhere else to write, keep the source folder.
        if (string.IsNullOrEmpty(_pathsConfiguration.Metadatas))
            return true;

        var root = Path.GetFullPath(_pathsConfiguration.Metadatas)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    public async Task Handle(GenerateMetadataPictureVariantsCommand request, CancellationToken cancellationToken)
    {
        var picture = await _context.MetadataPictures
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == request.MetadataPictureId, cancellationToken);

        // Picture may have been replaced/removed while this task was queued.
        if (picture is null)
        {
            _logger.LogInformation(
                "MetadataPicture {PictureId} no longer exists; cancelling variant generation",
                request.MetadataPictureId);
            _taskExecutionContext.Cancel("Metadata picture no longer exists.");
            return;
        }

        if (picture.LocalPath is null || !File.Exists(picture.LocalPath))
        {
            _logger.LogWarning("MetadataPicture {Id} has no local file, skipping variant generation", picture.Id);
            return;
        }

        if (picture.Type == MetadataPictureType.Thumbnail)
        {
            return;
        }

        var sourcePath = picture.LocalPath;
        var isSvgOriginal = _imageProcessor.IsSvgFile(sourcePath);
        // A picture that lives in a library folder (cover.jpg next to the tracks) is read-only
        // for K7: its variants go under the metadata root and the original is never converted
        // or deleted. Only files K7 downloaded itself are rewritten in place.
        var isManagedSource = IsUnderMetadataRoot(sourcePath);
        var directory = isManagedSource
            ? Path.GetDirectoryName(sourcePath)!
            : Path.Combine(_pathsConfiguration.Metadatas, LocalPictureVariantsFolder);
        if (!isManagedSource)
            Directory.CreateDirectory(directory);
        var existingSizes = picture.Variants.Select(v => v.Size).ToHashSet();

        if (picture.OriginalWidth is null || picture.OriginalHeight is null)
        {
            var dimensions = _imageProcessor.TryGetImageDimensions(sourcePath);
            if (dimensions is not null)
            {
                picture.OriginalWidth = dimensions.Value.Width;
                picture.OriginalHeight = dimensions.Value.Height;
            }
        }

        // Generate variants from the source file before converting the original to WebP,
        // so Medium/Small are not a second lossy pass over an already-encoded WebP.
        foreach (MetadataPictureSize size in Enum.GetValues<MetadataPictureSize>())
        {
            if (!MetadataPictureVariantRules.TryGetTargetWidth(picture.Type, size, out var maxWidth))
                continue;

            if (existingSizes.Contains(size))
                continue;

            if (picture.OriginalWidth is > 0
                && !MetadataPictureVariantRules.ShouldGenerateVariant(picture.Type, size, picture.OriginalWidth.Value))
            {
                _logger.LogDebug(
                    "Skipping {Size} variant for MetadataPicture {PictureId}: original width {OriginalWidth}px is not larger than target",
                    size,
                    picture.Id,
                    picture.OriginalWidth);
                continue;
            }

            var variantFileName = $"{picture.Id}_{size.ToString().ToLowerInvariant()}.webp";
            var variantPath = Path.Combine(directory, variantFileName);

            try
            {
                if (isSvgOriginal)
                {
                    await _imageProcessor.RasterizeSvgToWebPAsync(sourcePath, variantPath, maxWidth, cancellationToken: cancellationToken);
                }
                else
                {
                    await _imageProcessor.ResizeAsync(sourcePath, variantPath, maxWidth, cancellationToken: cancellationToken);
                }

                var variant = new MetadataPictureVariant
                {
                    Id = Guid.NewGuid(),
                    MetadataPictureId = picture.Id,
                    Size = size,
                    LocalPath = variantPath,
                    Width = maxWidth,
                    Height = 0,
                };

                _context.MetadataPictureVariants.Add(variant);

                _logger.LogInformation(
                    "Generated {Size} variant for MetadataPicture {PictureId} ({Type})",
                    size, picture.Id, picture.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to generate {Size} variant for MetadataPicture {PictureId}",
                    size, picture.Id);
            }
        }

        if (isManagedSource && !isSvgOriginal && !sourcePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            var webpPath = Path.ChangeExtension(sourcePath, ".webp");
            try
            {
                await _imageProcessor.ConvertToWebPAsync(sourcePath, webpPath, cancellationToken: cancellationToken);
                File.Delete(sourcePath);
                picture.LocalPath = webpPath;
                sourcePath = webpPath;
                _logger.LogInformation("Converted original MetadataPicture {Id} to WebP", picture.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert MetadataPicture {Id} to WebP, continuing with original", picture.Id);
            }
        }

        if (picture.DominantColor is null)
        {
            try
            {
                picture.DominantColor = await _imageProcessor.ExtractDominantColorAsync(sourcePath, cancellationToken);
                if (picture.DominantColor is not null)
                    _logger.LogInformation("Extracted dominant color {Color} for MetadataPicture {PictureId}", picture.DominantColor, picture.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract dominant color for MetadataPicture {PictureId}", picture.Id);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _pictureReadyNotifier.NotifyIfMediaPictureReadyAsync(picture.Id, cancellationToken);
    }
}
