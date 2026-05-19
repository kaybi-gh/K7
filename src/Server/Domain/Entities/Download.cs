using K7.Server.Domain.Common;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities;

public class Download : BaseAuditableEntity
{
    public Guid IndexedFileId { get; set; }
    public IndexedFile IndexedFile { get; set; } = null!;

    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public DownloadStatus Status { get; set; } = DownloadStatus.Pending;
    public string? OutputPath { get; set; }
    public string? ContentType { get; set; }
    public long? FileSize { get; set; }

    public int? AudioTrackIndex { get; set; }
    public string? SubtitleTrackIndices { get; set; }

    public bool IsDirectStream { get; set; }
    public DateTimeOffset? ReadyAt { get; set; }
    public string? FailureReason { get; set; }
}
