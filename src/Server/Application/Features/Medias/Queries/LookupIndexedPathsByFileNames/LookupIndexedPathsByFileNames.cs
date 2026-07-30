using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Responses;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Medias.Queries.LookupIndexedPathsByFileNames;

[Authorize(Roles = Roles.Administrator)]
public record LookupIndexedPathsByFileNamesQuery : IRequest<List<IndexedPathByFileNameResult>>
{
    public required IReadOnlyList<string> FileNames { get; init; }
}

public class LookupIndexedPathsByFileNamesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<LookupIndexedPathsByFileNamesQuery, List<IndexedPathByFileNameResult>>
{
    public async Task<List<IndexedPathByFileNameResult>> Handle(
        LookupIndexedPathsByFileNamesQuery request,
        CancellationToken cancellationToken)
    {
        var names = request.FileNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(200)
            .ToList();

        if (names.Count == 0)
            return [];

        // IndexedFile.Name is stored without extension; Plex paths usually include one.
        var stems = names
            .Select(GetStem)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var stemsLower = stems.Select(s => s.ToLowerInvariant()).ToList();

        var files = await context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.MediaId != null && stemsLower.Contains(f.Name.ToLower()))
            .Select(f => new { f.Name, f.Extension, f.Path })
            .ToListAsync(cancellationToken);

        return names
            .Select(name =>
            {
                var stem = GetStem(name);
                var paths = files
                    .Where(f =>
                        string.Equals(f.Name, stem, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(f.Name + f.Extension, name, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(PathFileName(f.Path), name, StringComparison.OrdinalIgnoreCase))
                    .Select(f => NormalizePath(f.Path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(20)
                    .ToList();

                return new IndexedPathByFileNameResult
                {
                    FileName = name,
                    Paths = paths
                };
            })
            .ToList();
    }

    private static string GetStem(string fileName)
    {
        var leaf = fileName.Replace('\\', '/').Split('/').Last();
        var dot = leaf.LastIndexOf('.');
        return dot > 0 ? leaf[..dot] : leaf;
    }

    private static string PathFileName(string path) =>
        path.Replace('\\', '/').Split('/').Last();

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').Trim();
}
