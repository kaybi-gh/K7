using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.Commands.DeleteMediaPicture;

[Authorize(Roles = Roles.Administrator)]
public record DeleteMediaPictureCommand : IRequest
{
    public required Guid MediaId { get; init; }
    public required Guid PictureId { get; init; }
}

public class DeleteMediaPictureCommandHandler : IRequestHandler<DeleteMediaPictureCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<DeleteMediaPictureCommandHandler> _logger;

    public DeleteMediaPictureCommandHandler(
        IApplicationDbContext context,
        ILogger<DeleteMediaPictureCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(DeleteMediaPictureCommand request, CancellationToken cancellationToken)
    {
        var picture = await _context.MetadataPictures
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == request.PictureId && p.MediaId == request.MediaId, cancellationToken);

        Guard.Against.NotFound(request.PictureId, picture);

        // Delete variant files
        foreach (var variant in picture.Variants)
        {
            if (File.Exists(variant.LocalPath))
            {
                File.Delete(variant.LocalPath);
            }
        }

        // Delete original file
        if (picture.LocalPath is not null && File.Exists(picture.LocalPath))
        {
            File.Delete(picture.LocalPath);
        }

        _context.MetadataPictures.Remove(picture);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted picture {PictureId} from media {MediaId}", request.PictureId, request.MediaId);
    }
}
