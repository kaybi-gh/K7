using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.MetadataPictures.Commands.GenerateAllMissingMetadataPictureVariants;

public record GenerateAllMissingMetadataPictureVariantsCommand : IRequest<int>;

public class GenerateAllMissingMetadataPictureVariantsCommandHandler
    : IRequestHandler<GenerateAllMissingMetadataPictureVariantsCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly ILogger<GenerateAllMissingMetadataPictureVariantsCommandHandler> _logger;

    public GenerateAllMissingMetadataPictureVariantsCommandHandler(
        IApplicationDbContext context,
        ISender sender,
        ILogger<GenerateAllMissingMetadataPictureVariantsCommandHandler> logger)
    {
        _context = context;
        _sender = sender;
        _logger = logger;
    }

    public async Task<int> Handle(GenerateAllMissingMetadataPictureVariantsCommand request, CancellationToken cancellationToken)
    {
        var pictureIds = await _context.MetadataPictures
            .Where(p => p.LocalPath != null
                && p.Type != MetadataPictureType.Thumbnail
                && !p.Variants.Any())
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Enqueuing variant generation for {Count} metadata pictures", pictureIds.Count);

        foreach (var pictureId in pictureIds)
        {
            await _sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new GenerateMetadataPictureVariantsCommand
                {
                    MetadataPictureId = pictureId
                },
                Priority = BackgroundTaskPriority.Lowest,
                TargetEntityId = pictureId,
                TargetEntityTypeName = nameof(MetadataPicture),
                MaxRetryCount = 3
            }, cancellationToken);
        }

        return pictureIds.Count;
    }
}
