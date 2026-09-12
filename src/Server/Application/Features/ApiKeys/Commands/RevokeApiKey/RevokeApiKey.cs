using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.ApiKeys.Commands.RevokeApiKey;

[Authorize(Roles = Roles.Administrator)]
public record RevokeApiKeyCommand(Guid Id) : IRequest;

public class RevokeApiKeyCommandHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<RevokeApiKeyCommand>
{
    public async Task Handle(RevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.ApiKeys
            .FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(request.Id.ToString(), nameof(ApiKey));

        var createdByUserName = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == entity.CreatedByUserId)
            .Select(u => u.IdentityUserId)
            .FirstOrDefaultAsync(cancellationToken) is { } identityUserId
            ? await identityService.GetUserNameAsync(identityUserId)
            : null;

        entity.AddDomainEvent(new ApiKeyRevokedEvent(
            entity.Id,
            entity.Name,
            entity.Scope.ToString(),
            entity.KeyPrefix,
            entity.CreatedByUserId,
            createdByUserName));

        context.ApiKeys.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
