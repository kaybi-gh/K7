using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Http;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetDirectStream;

public record GetDirectStreamQuery(Guid Id) : IRequest<IResult>;

public class GetDirectStreamQueryHandler : IRequestHandler<GetDirectStreamQuery, IResult>
{
    private readonly IApplicationDbContext _context;

    public GetDirectStreamQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetDirectStreamQuery query, CancellationToken cancellationToken)
    {
        var entity = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);

        Guard.Against.NotFound(query.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
        {
            return Results.NotFound();
        }

        var container = entity.FileMetadata?.Container;
        var mimeType = container != null && Constants.ContainerMimeTypeMapping.TryGetValue(container, out var mime)
            ? mime
            : "application/octet-stream";

        return Results.Stream(file.OpenRead(), contentType: mimeType, enableRangeProcessing: true);
    }
}
