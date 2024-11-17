using K7.Server.Application.Common.Interfaces;
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
            .FindAsync([query.Id], cancellationToken);

        Guard.Against.NotFound(query.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
        {
            return Results.NotFound();
        }

        return Results.Stream(file.OpenRead(), contentType: file.Extension switch
        {
            ".mkv" => "video/x-matroska",
            ".mpd" => "video/vnd.mpeg.dash.mpd",
            ".mpegts" => "video/mp2t",
            _ => $"video/{file.Extension.TrimStart('.')}"
        },
        enableRangeProcessing: true);
        // TODO - Manage mime-type in database or with a better class
    }
}
