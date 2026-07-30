using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Responses;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Medias.Queries.LookupMediasByPaths;

[Authorize(Roles = Roles.Administrator)]
public record LookupMediasByPathsQuery : IRequest<List<PathMatchResult>>
{
    public required IReadOnlyList<string> Paths { get; init; }
}

public class LookupMediasByPathsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<LookupMediasByPathsQuery, List<PathMatchResult>>
{
    public async Task<List<PathMatchResult>> Handle(LookupMediasByPathsQuery request, CancellationToken cancellationToken)
    {
        var normalized = request.Paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
            return [];

        var results = new List<PathMatchResult>(normalized.Count);
        foreach (var batch in normalized.Chunk(500))
        {
            var batchList = batch.ToList();
            var lowerBatch = batchList.Select(p => p.ToLowerInvariant()).ToList();

            var files = await context.IndexedFiles
                .AsNoTracking()
                .Where(f => f.MediaId != null && lowerBatch.Contains(f.Path.ToLower()))
                .Select(f => new { f.Path, f.MediaId })
                .ToListAsync(cancellationToken);

            var byPath = files
                .GroupBy(f => NormalizePath(f.Path), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().MediaId, StringComparer.OrdinalIgnoreCase);

            foreach (var path in batchList)
            {
                results.Add(new PathMatchResult
                {
                    Path = path,
                    MediaId = byPath.GetValueOrDefault(path)
                });
            }
        }

        return results;
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').Trim();
}
