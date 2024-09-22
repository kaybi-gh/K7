using System.Text;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace MediaServer.Application.Features.IndexedFiles.Queries.GetHlsStreamSegment;

public record GetHlsStreamSegmentQuery(Guid Id, string VideoResolutionIdentifier, int segmentId) : IRequest<IResult>;

public class GetHlsStreamSegmentQueryHandler : IRequestHandler<GetHlsStreamSegmentQuery, IResult>
{
    private readonly IApplicationDbContext _context;
    private readonly PathsConfiguration _pathsConfiguration;

    public GetHlsStreamSegmentQueryHandler(IApplicationDbContext context, IOptions<PathsConfiguration> pathsConfiguration)
    {
        _context = context;
        _pathsConfiguration = pathsConfiguration.Value;
    }

    public async Task<IResult> Handle(GetHlsStreamSegmentQuery query, CancellationToken cancellationToken)
    {
        var segmentPath = Path.Combine(_pathsConfiguration.Transcoding, $"{query.Id}", query.VideoResolutionIdentifier, $"{query.segmentId}.ts");
        var file = new FileInfo(segmentPath);

        if (file.Exists)
        {
            // TODO - Generate next segments for buffering
            return Results.File(file.OpenRead(), contentType: "video/mp2t");
        }

        // TODO - Generate current segment and next ones for buffering

        var segments = await _context.HlsSegments
            .Where(x => x.IndexedFileId == query.Id)
            .Where(x => x.Number >= query.segmentId)
            .Take(10)
            .ToListAsync();

        Guard.Against.NullOrEmpty(segments);

        throw new NotImplementedException();
    }

}
