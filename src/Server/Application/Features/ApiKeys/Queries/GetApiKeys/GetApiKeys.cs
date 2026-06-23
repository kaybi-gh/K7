using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.ApiKeys.Queries.GetApiKeys;

[Authorize(Roles = Roles.Administrator)]
public record GetApiKeysQuery : IRequest<List<ApiKeyDto>>;

public class GetApiKeysQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetApiKeysQuery, List<ApiKeyDto>>
{
    public async Task<List<ApiKeyDto>> Handle(GetApiKeysQuery request, CancellationToken cancellationToken)
    {
        return await context.ApiKeys
            .OrderByDescending(k => k.Created)
            .Select(k => new ApiKeyDto
            {
                Id = k.Id,
                Name = k.Name,
                KeyPrefix = k.KeyPrefix,
                Scope = k.Scope,
                CreatedAt = k.Created,
                LastUsedAt = k.LastUsedAt,
                ExpiresAt = k.ExpiresAt
            })
            .ToListAsync(cancellationToken);
    }
}
