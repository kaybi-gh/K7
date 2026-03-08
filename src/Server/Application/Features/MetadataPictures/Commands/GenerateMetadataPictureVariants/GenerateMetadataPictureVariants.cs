using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Configuration;
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

    /// <summary>
    /// Target widths per picture type and size.
    /// Key: (PictureType, Size) → maxWidth in pixels.
    /// </summary>
    private static readonly Dictionary<(MetadataPictureType, MetadataPictureSize), int> VariantWidths = new()
    {
        // Posters (2:3 ratio)
        [(MetadataPictureType.Poster, MetadataPictureSize.Small)] = 200,
        [(MetadataPictureType.Poster, MetadataPictureSize.Medium)] = 342,

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
        ILogger<GenerateMetadataPictureVariantsCommandHandler> logger)
    {
        _context = context;
        _imageProcessor = imageProcessor;
        _pathsConfiguration = pathsConfiguration.Value;
        _logger = logger;
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

        // Thumbnails don't need variants
        if (picture.Type == MetadataPictureType.Thumbnail)
        {
            return;
        }

        var directory = Path.GetDirectoryName(picture.LocalPath)!;
        var existingSizes = picture.Variants.Select(v => v.Size).ToHashSet();

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
                await _imageProcessor.ResizeAsync(picture.LocalPath, variantPath, maxWidth, cancellationToken: cancellationToken);

                var variant = new MetadataPictureVariant
                {
                    Id = Guid.NewGuid(),
                    MetadataPictureId = picture.Id,
                    Size = size,
                    LocalPath = variantPath,
                    Width = maxWidth,
                    Height = 0, // Computed by ffmpeg (aspect ratio preserved)
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

        await _context.SaveChangesAsync(cancellationToken);
    }
}
