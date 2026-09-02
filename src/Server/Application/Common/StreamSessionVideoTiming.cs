using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Server.Application.Common;

public static class StreamSessionVideoTiming
{
    public static void CopyFrom(StreamingSessionDto session, IEnumerable<VideoFileTrackDto>? tracks)
    {
        var track = tracks?
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Index)
            .FirstOrDefault();
        if (track is null)
            return;

        session.SourceFrameRate = track.FrameRate;
        session.SourceVideoWidth = track.Width;
        session.SourceVideoHeight = track.Height;
    }
}
