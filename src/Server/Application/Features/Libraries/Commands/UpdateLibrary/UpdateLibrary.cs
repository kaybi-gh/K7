using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Libraries.Commands.UpdateLibrary;

[Authorize(Roles = Roles.Administrator)]
public record UpdateLibraryCommand : IRequest
{
    public Guid Id { get; init; }

    public string? Title { get; init; }
}

public class UpdateLibraryCommandHandler : IRequestHandler<UpdateLibraryCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateLibraryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateLibraryCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Libraries
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        entity.Title = request.Title ?? entity.Title;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
