using K7.Server.Domain.Common;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities;

public class StreamSession : BaseAuditableEntity
{
    public Guid IndexedFileId { get; set; }
    public IndexedFile IndexedFile { get; set; } = null!;

    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public PlaybackState State { get; set; } = PlaybackState.Idle;
    public double Position { get; set; }

    public string RootDirectory { get; set; } = string.Empty;
    public string PlaybackSettingsJson { get; set; } = "{}";
}
