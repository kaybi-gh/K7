using System.Linq.Expressions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Responses;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Medias.Queries.LookupMediasByExternalIds;

[Authorize(Roles = Roles.Administrator)]
public record LookupMediasByExternalIdsQuery : IRequest<List<ExternalIdMatchResult>>
{
    public required IReadOnlyList<LookupMediasByExternalIdsRequest.ExternalIdItem> Items { get; init; }
}

public class LookupMediasByExternalIdsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<LookupMediasByExternalIdsQuery, List<ExternalIdMatchResult>>
{
    public async Task<List<ExternalIdMatchResult>> Handle(LookupMediasByExternalIdsQuery request, CancellationToken cancellationToken)
    {
        var results = new List<ExternalIdMatchResult>(request.Items.Count);

        foreach (var batch in request.Items.Chunk(500))
        {
            // Build an OR predicate: (provider == "x" && value == "1") || (provider == "y" && value == "2") || ...
            // EF Core can't translate .Any() with in-memory complex objects, so we build the expression manually.
            var parameter = Expression.Parameter(typeof(ExternalId), "e");
            Expression? predicate = null;

            foreach (var item in batch)
            {
                var providerEqual = Expression.Equal(
                    Expression.Property(parameter, nameof(ExternalId.ProviderName)),
                    Expression.Constant(item.Provider));
                var valueEqual = Expression.Equal(
                    Expression.Property(parameter, nameof(ExternalId.Value)),
                    Expression.Constant(item.Value));
                var pair = Expression.AndAlso(providerEqual, valueEqual);

                predicate = predicate is null ? pair : Expression.OrElse(predicate, pair);
            }

            var mediaIdNotNull = Expression.NotEqual(
                Expression.Property(parameter, nameof(ExternalId.MediaId)),
                Expression.Constant(null, typeof(int?)));

            var fullPredicate = Expression.AndAlso(mediaIdNotNull, predicate!);
            var lambda = Expression.Lambda<Func<ExternalId, bool>>(fullPredicate, parameter);

            var matches = await context.ExternalIds
                .Where(lambda)
                .Select(e => new { e.ProviderName, e.Value, e.MediaId })
                .ToListAsync(cancellationToken);

            var matchLookup = matches
                .GroupBy(m => (m.ProviderName, m.Value))
                .ToDictionary(g => g.Key, g => g.First().MediaId);

            foreach (var item in batch)
            {
                results.Add(new ExternalIdMatchResult
                {
                    Provider = item.Provider,
                    Value = item.Value,
                    MediaId = matchLookup.GetValueOrDefault((item.Provider, item.Value))
                });
            }
        }

        return results;
    }
}
