using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.ApiKeys.Commands.CreateApiKey;

[Authorize(Roles = Roles.Administrator)]
public record CreateApiKeyCommand : IRequest<CreateApiKeyResponse>
{
    public required string Name { get; init; }
    public ApiKeyScope Scope { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

public class CreateApiKeyCommandHandler(
    IApplicationDbContext context,
    IApiKeyService apiKeyService,
    IUser user,
    IIdentityService identityService)
    : IRequestHandler<CreateApiKeyCommand, CreateApiKeyResponse>
{
    public async Task<CreateApiKeyResponse> Handle(CreateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var (fullKey, keyHash, keyPrefix) = apiKeyService.GenerateKey();
        var createdByUserId = user.Id!.Value;
        var createdByUserName = user.IdentityId is not null
            ? await identityService.GetUserNameAsync(user.IdentityId)
            : null;

        var entity = new ApiKey
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            Scope = request.Scope,
            ExpiresAt = request.ExpiresAt,
            CreatedByUserId = createdByUserId
        };

        entity.AddDomainEvent(new ApiKeyCreatedEvent(
            entity.Id,
            entity.Name,
            entity.Scope.ToString(),
            entity.KeyPrefix,
            createdByUserId,
            createdByUserName));

        context.ApiKeys.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return new CreateApiKeyResponse
        {
            Id = entity.Id,
            FullKey = fullKey
        };
    }
}
