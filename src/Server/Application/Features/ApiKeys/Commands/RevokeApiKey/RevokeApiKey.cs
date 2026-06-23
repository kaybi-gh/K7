using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.Features.ApiKeys.Commands.RevokeApiKey;

[Authorize(Roles = Roles.Administrator)]
public record RevokeApiKeyCommand(Guid Id) : IRequest;

public class RevokeApiKeyCommandHandler(IApplicationDbContext context)
    : IRequestHandler<RevokeApiKeyCommand>
{
    public async Task Handle(RevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.ApiKeys
            .FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(request.Id.ToString(), nameof(ApiKey));

        context.ApiKeys.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
