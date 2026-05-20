using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Persons.Commands.DeletePersonPicture;

[Authorize(Roles = Roles.Administrator)]
public record DeletePersonPictureCommand : IRequest
{
    public required Guid PersonId { get; init; }
}

public class DeletePersonPictureCommandHandler : IRequestHandler<DeletePersonPictureCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<DeletePersonPictureCommandHandler> _logger;

    public DeletePersonPictureCommandHandler(
        IApplicationDbContext context,
        ILogger<DeletePersonPictureCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(DeletePersonPictureCommand request, CancellationToken cancellationToken)
    {
        var person = await _context.Persons
            .Include(p => p.PortraitPicture)
                .ThenInclude(p => p!.Variants)
            .FirstOrDefaultAsync(p => p.Id == request.PersonId, cancellationToken);

        Guard.Against.NotFound(request.PersonId, person);

        if (person.PortraitPicture is null)
            return;

        var picture = person.PortraitPicture;

        foreach (var variant in picture.Variants)
        {
            if (File.Exists(variant.LocalPath))
            {
                File.Delete(variant.LocalPath);
            }
        }

        if (picture.LocalPath is not null && File.Exists(picture.LocalPath))
        {
            File.Delete(picture.LocalPath);
        }

        _context.MetadataPictures.Remove(picture);
        person.PortraitPicture = null;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted portrait picture from person {PersonId}", request.PersonId);
    }
}
