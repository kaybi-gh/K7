using MediaServer.Application.Common.Interfaces;
using MediaServer.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace MediaServer.Application.Features.IndexedFiles.Queries.GetHlsStreamSegment;

public static class GetHlsStreamSegmentQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/{quality}/segments/{segmentId}.ts";

    public static string Build(GetHlsStreamSegmentQuery query) => Route
        .Replace("{id}", $"{query.Id}")
        .Replace("{segmentId}", $"{query.SegmentId}")
        .Replace("{quality}", query.VideoResolutionIdentifier);

    public static string Build(Guid id, string videoResolutionIdentifier, int segmentId) => Route
        .Replace("{id}", $"{id}")
        .Replace("{segmentId}", $"{segmentId}")
        .Replace("{quality}", videoResolutionIdentifier);
}

public record GetHlsStreamSegmentQuery(Guid Id, string VideoResolutionIdentifier, int SegmentId) : IRequest<IResult>;

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
        var segmentPath = Path.Combine(_pathsConfiguration.Transcoding, $"{query.Id}", query.VideoResolutionIdentifier, $"{query.SegmentId}.ts");
        var file = new FileInfo(segmentPath);

        if (file.Exists)
        {
            // TODO - Generate next segments for buffering
            return Results.File(file.OpenRead(), contentType: "video/mp2t");
        }

        // TODO - Generate current segment and next ones for buffering

        var segments = await _context.HlsSegments
            .Where(x => x.IndexedFileId == query.Id)
            .Where(x => x.Number >= query.SegmentId)
            .Take(10)
            .ToListAsync();

        Guard.Against.NullOrEmpty(segments);

        throw new NotImplementedException();
    }

}
