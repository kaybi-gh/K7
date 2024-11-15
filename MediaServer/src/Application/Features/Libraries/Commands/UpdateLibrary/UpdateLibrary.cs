using MediaServer.Application.Common.Interfaces;

namespace MediaServer.Application.Features.Libraries.Commands.UpdateLibrary;

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
