using K7.Server.Application.Common.Configuration;
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

    /// <summary>
    /// Target widths per picture type and size.
    /// Key: (PictureType, Size) -> maxWidth in pixels.
    /// </summary>
    private static readonly Dictionary<(MetadataPictureType, MetadataPictureSize), int> VariantWidths = new()
    {
        // Posters and covers (2:3 ratio)
        [(MetadataPictureType.Poster, MetadataPictureSize.Small)] = 200,
        [(MetadataPictureType.Poster, MetadataPictureSize.Medium)] = 342,
        [(MetadataPictureType.Cover, MetadataPictureSize.Small)] = 200,
        [(MetadataPictureType.Cover, MetadataPictureSize.Medium)] = 342,

        // Backdrops (16:9 ratio)
        [(MetadataPictureType.Backdrop, MetadataPictureSize.Small)] = 780,
        [(MetadataPictureType.Backdrop, MetadataPictureSize.Medium)] = 1280,

        // Portraits (2:3 ratio)
        [(MetadataPictureType.Portrait, MetadataPictureSize.Small)] = 185,
        [(MetadataPictureType.Portrait, MetadataPictureSize.Medium)] = 342,

        // Logos
        [(MetadataPictureType.Logo, MetadataPictureSize.Small)] = 200,
        [(MetadataPictureType.Logo, MetadataPictureSize.Medium)] = 400,
    };

    public GenerateMetadataPictureVariantsCommandHandler(
        IApplicationDbContext context,
        IImageProcessor imageProcessor,
        IOptions<PathsConfiguration> pathsConfiguration,
        ILogger<GenerateMetadataPictureVariantsCommandHandler> logger,
        MediaPictureReadyNotifier pictureReadyNotifier)
    {
        _context = context;
        _imageProcessor = imageProcessor;
        _pathsConfiguration = pathsConfiguration.Value;
        _logger = logger;
        _pictureReadyNotifier = pictureReadyNotifier;
    }

    public async Task Handle(GenerateMetadataPictureVariantsCommand request, CancellationToken cancellationToken)
    {
        var picture = await _context.MetadataPictures
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == request.MetadataPictureId, cancellationToken);

        Guard.Against.NotFound(request.MetadataPictureId, picture);

        if (picture.LocalPath is null || !File.Exists(picture.LocalPath))
        {
            _logger.LogWarning("MetadataPicture {Id} has no local file, skipping variant generation", picture.Id);
            return;
        }

        if (picture.Type == MetadataPictureType.Thumbnail)
        {
            return;
        }

        var isSvgOriginal = _imageProcessor.IsSvgFile(picture.LocalPath);

        if (!isSvgOriginal && !picture.LocalPath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            var webpPath = Path.ChangeExtension(picture.LocalPath, ".webp");
            try
            {
                await _imageProcessor.ConvertToWebPAsync(picture.LocalPath, webpPath, cancellationToken: cancellationToken);
                File.Delete(picture.LocalPath);
                picture.LocalPath = webpPath;
                _logger.LogInformation("Converted original MetadataPicture {Id} to WebP", picture.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert MetadataPicture {Id} to WebP, continuing with original", picture.Id);
            }
        }

        var directory = Path.GetDirectoryName(picture.LocalPath)!;
        var existingSizes = picture.Variants.Select(v => v.Size).ToHashSet();
        var sourcePath = picture.LocalPath;

        foreach (var ((pictureType, size), maxWidth) in VariantWidths)
        {
            if (pictureType != picture.Type)
                continue;

            if (existingSizes.Contains(size))
                continue;

            var variantFileName = $"{picture.Id}_{size.ToString().ToLowerInvariant()}.webp";
            var variantPath = Path.Combine(directory, variantFileName);

            try
            {
                if (_imageProcessor.IsSvgFile(sourcePath))
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
