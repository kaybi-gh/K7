using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Users;

public abstract class PlaybackBookmark : BaseAuditableEntity
{
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public Guid? SharedProfileId { get; set; }
    public SharedProfile? SharedProfile { get; set; }

    public PlaybackBookmarkKind Kind { get; protected set; }

    public DateTime UpdatedAt { get; set; }
}

public sealed class ItemPlaybackBookmark : PlaybackBookmark
{
    public Guid MediaId { get; set; }
    public BaseMedia Media { get; set; } = null!;

    public double PositionSeconds { get; set; }
    public double DurationSeconds { get; set; }

    public ItemPlaybackBookmark()
    {
        Kind = PlaybackBookmarkKind.Item;
    }

    public double ProgressPercentage =>
        DurationSeconds > 0
            ? Math.Clamp(PositionSeconds / DurationSeconds * 100, 0, 100)
            : 0;
}

public sealed class SeriesPlaybackBookmark : PlaybackBookmark
{
    public Guid SerieId { get; set; }
    public Serie Serie { get; set; } = null!;

    public Guid LastCompletedEpisodeId { get; set; }
    public SerieEpisode LastCompletedEpisode { get; set; } = null!;

    public Guid? NextEpisodeId { get; set; }
    public SerieEpisode? NextEpisode { get; set; }

    /// <summary>When the user last finished an episode in this series.</summary>
    public DateTime ActivityAt { get; set; }

    /// <summary>When the current next-up target became available (scan or completion).</summary>
    public DateTime NextEpisodeAvailableAt { get; set; }

    public SeriesPlaybackBookmark()
    {
        Kind = PlaybackBookmarkKind.Series;
    }
}
