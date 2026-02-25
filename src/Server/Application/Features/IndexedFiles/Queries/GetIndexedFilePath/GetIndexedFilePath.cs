using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetIndexedFilePath;

public record GetIndexedFilePathQuery
    : IRequest<string>
{
    public required Guid Id { get; init; }
}

public sealed class GetIndexedFilePathQueryHandler
    : IRequestHandler<GetIndexedFilePathQuery, string>
{
    private readonly IApplicationDbContext _context;

    public GetIndexedFilePathQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> Handle(GetIndexedFilePathQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.IndexedFiles
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);

        return entity.Path!;
    }
}
