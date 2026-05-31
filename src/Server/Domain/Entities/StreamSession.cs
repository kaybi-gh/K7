using K7.Server.Domain.Common;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities;

public class StreamSession : BaseAuditableEntity
{
    public Guid? IndexedFileId { get; set; }
    public IndexedFile? IndexedFile { get; set; }

    public Guid? RemoteIndexedFileId { get; set; }
    public RemoteIndexedFile? RemoteIndexedFile { get; set; }

    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public Guid? PeerServerId { get; set; }
    public PeerServer? PeerServer { get; set; }

    public Guid? RemoteSessionId { get; set; }

    public PlaybackState State { get; set; } = PlaybackState.Idle;
    public double Position { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    public string RootDirectory { get; set; } = string.Empty;
    public string PlaybackSettingsJson { get; set; } = "{}";
}
