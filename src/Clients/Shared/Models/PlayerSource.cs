using K7.Clients.Shared.Helpers;
using K7.Shared.Dtos;

namespace K7.Clients.Shared.Models;

public class PlayerSource
{
    public Guid? MediaId { get; set; }
    public Guid? StreamSessionId { get; set; }
    public Guid? IndexedFileId { get; set; }
    public string? Url { get; set; }
    public string? MimeType { get; set; }
    public double? PendingSeekTime { get; set; }
    /// <summary>File duration from metadata (seconds). Used so the seekbar total is known before VLC Length.</summary>
    public double? KnownDurationSeconds { get; set; }
    public string? ThumbnailsUrl { get; set; }
    public IReadOnlyList<K7.Shared.Dtos.Entities.Metadatas.Files.ChapterMarkerDto>? Chapters { get; set; }
    public string? Title { get; set; }
    public string? CoverUrl { get; set; }
    public StreamDecisionDto? StreamDecision { get; set; }

    /// <summary>Content fps from the file (ffprobe), used for HDMI AFR. 0 when unknown.</summary>
    public float SourceFrameRate { get; set; }

    /// <summary>Source video pixel size for HDMI mode picking. 0 when unknown.</summary>
    public int SourceVideoWidth { get; set; }

    public int SourceVideoHeight { get; set; }

    public void CopyVideoTimingFrom(PlayerSource? other)
    {
        if (other is null)
            return;

        SourceFrameRate = other.SourceFrameRate;
        SourceVideoWidth = other.SourceVideoWidth;
        SourceVideoHeight = other.SourceVideoHeight;
        StreamDecision = other.StreamDecision;
    }

    public void CopyVideoTimingFrom(float? fps, int? width, int? height)
    {
        SourceFrameRate = fps ?? 0;
        SourceVideoWidth = width ?? 0;
        SourceVideoHeight = height ?? 0;
    }

    public void ApplyStreamDecision(StreamDecisionDto? decision, bool isOriginalQuality)
    {
        StreamDecision = StreamDecisionPlayback.Align(decision, Url, MimeType, isOriginalQuality);
    }
}
