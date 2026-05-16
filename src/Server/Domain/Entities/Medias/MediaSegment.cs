using K7.Server.Domain.Common;

namespace K7.Server.Domain.Entities.Medias;

public class MediaSegment : BaseEntity
{
    public required Guid MediaId { get; set; }
    public BaseMedia Media { get; set; } = null!;

    public required MediaSegmentType Type { get; set; }
    public required long StartMs { get; set; }
    public required long EndMs { get; set; }
    public required DateTimeOffset DetectedAt { get; set; }
}
