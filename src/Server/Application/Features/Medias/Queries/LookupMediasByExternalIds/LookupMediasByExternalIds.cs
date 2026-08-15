using System.Linq.Expressions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Shared;
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
            // Provider comparison is case-insensitive via ToLower.
            var parameter = Expression.Parameter(typeof(ExternalId), "e");
            Expression? predicate = null;
            var providerProperty = Expression.Property(parameter, nameof(ExternalId.ProviderName));
            var toLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;

            foreach (var item in batch)
            {
                var providerEqual = Expression.Equal(
                    Expression.Call(providerProperty, toLower),
                    Expression.Constant(item.Provider.ToLowerInvariant()));
                var valueEqual = Expression.Equal(
                    Expression.Property(parameter, nameof(ExternalId.Value)),
                    Expression.Constant(item.Value));
                var pair = Expression.AndAlso(providerEqual, valueEqual);

                predicate = predicate is null ? pair : Expression.OrElse(predicate, pair);
            }

            var mediaIdNotNull = Expression.NotEqual(
                Expression.Property(parameter, nameof(ExternalId.MediaId)),
                Expression.Constant(null, typeof(Guid?)));

            var fullPredicate = Expression.AndAlso(mediaIdNotNull, predicate!);
            var lambda = Expression.Lambda<Func<ExternalId, bool>>(fullPredicate, parameter);

            var matches = await context.ExternalIds
                .Where(lambda)
                .Select(e => new
                {
                    e.ProviderName,
                    e.Value,
                    e.MediaId,
                    Type = e.Media!.Type,
                    HasIndexedFiles = e.Media != null && e.Media.IndexedFiles.Any()
                })
                .ToListAsync(cancellationToken);

            // Prefer playable medias when the same external id exists on a virtual leftover.
            var matchLookup = matches
                .Where(m => m.MediaId.HasValue)
                .GroupBy(m => (Provider: m.ProviderName.ToLowerInvariant(), m.Value))
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.HasIndexedFiles).ThenBy(x => x.MediaId).First());

            foreach (var item in batch)
            {
                matchLookup.TryGetValue((item.Provider.ToLowerInvariant(), item.Value), out var match);
                results.Add(new ExternalIdMatchResult
                {
                    Provider = item.Provider,
                    Value = item.Value,
                    MediaId = match?.MediaId,
                    MediaType = match is null ? null : ImportMediaTypeCompatibility.ToImportType(match.Type),
                    HasIndexedFiles = match?.HasIndexedFiles ?? false
                });
            }
        }

        return results;
    }
}
