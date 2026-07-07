using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MetadataPictures.Services;
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
    private readonly MetadataPictureDeletionService _pictureDeletionService;
    private readonly ILibraryNotifier _libraryNotifier;
    private readonly ILogger<DeleteMediaPictureCommandHandler> _logger;

    public DeleteMediaPictureCommandHandler(
        IApplicationDbContext context,
        MetadataPictureDeletionService pictureDeletionService,
        ILibraryNotifier libraryNotifier,
        ILogger<DeleteMediaPictureCommandHandler> logger)
    {
        _context = context;
        _pictureDeletionService = pictureDeletionService;
        _libraryNotifier = libraryNotifier;
        _logger = logger;
    }

    public async Task Handle(DeleteMediaPictureCommand request, CancellationToken cancellationToken)
    {
        var picture = await _context.MetadataPictures
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == request.PictureId && p.MediaId == request.MediaId, cancellationToken);

        Guard.Against.NotFound(request.PictureId, picture);

        _pictureDeletionService.Remove(picture);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted picture {PictureId} from media {MediaId}", request.PictureId, request.MediaId);

        await _libraryNotifier.NotifyMediaPicturesUpdatedAsync(request.MediaId, cancellationToken);
    }
}
