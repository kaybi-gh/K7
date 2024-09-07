using FFMpegCore;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Entities;

namespace MediaServer.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
public record ComputeHlsSegmentsCommand : IRequest
{
    public required Guid Id { get; set; }
    public required TimeSpan SegmentsDuration { get; init; }
}

public class ComputeHlsSegmentsCommandHandler : IRequestHandler<ComputeHlsSegmentsCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public ComputeHlsSegmentsCommandHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task Handle(ComputeHlsSegmentsCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.IndexedFiles
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
        {
            //return Results.NotFound();
        }

        var ffprobeResult = FFProbe.GetPackets(entity.Path);
        var keyframes = ffprobeResult.Packets
            .Where(x => x.StreamIndex == 0)
            .Where(x => x.CodecType == "video")
            .Where(x => x.Flags.Contains("K"))
            .ToList();

        var segments = new List<HlsSegment>();
        var currentSegment = new List<long>();
        var segmentId = 0;

        for (int i = 1; i < keyframes.Count; i++)
        {
            double currentDuration = 0.0;
            currentDuration += keyframes[i].Pts - keyframes[i - 1].Pts;
            currentSegment.Add(keyframes[i - 1].Pts);

            if (currentDuration >= request.SegmentsDuration.TotalMilliseconds)
            {
                foreach(var keyframe in currentSegment)
                {
                    var duration = TimeSpan.FromMilliseconds(currentDuration);
                    segments.Add(new HlsSegment()
                    {
                        VideoFileMetadataId = request.Id,
                        SegmentId = segmentId,
                        Duration = duration,
                        Keyframe = new TimeOnly(keyframe)
                    });
                }
                segmentId++;
                currentSegment.Clear();
                currentDuration = 0.0;
            }
        }

        _context.HlsSegments.AddRange(segments);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
