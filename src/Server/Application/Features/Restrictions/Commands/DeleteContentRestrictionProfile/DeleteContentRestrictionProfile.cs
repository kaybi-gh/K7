using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Restrictions.Commands.DeleteContentRestrictionProfile;

[Authorize(Roles = Roles.Administrator)]
public record DeleteContentRestrictionProfileCommand(Guid Id) : IRequest;

public class DeleteContentRestrictionProfileCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteContentRestrictionProfileCommand>
{
    public async Task Handle(DeleteContentRestrictionProfileCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.ContentRestrictionProfiles
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        context.ContentRestrictionProfiles.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
