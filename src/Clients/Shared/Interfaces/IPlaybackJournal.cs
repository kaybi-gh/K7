namespace K7.Clients.Shared.Interfaces;

public interface IPlaybackJournal
{
    Task RecordProgressAsync(Guid mediaId, Guid indexedFileId, double position, double duration, Guid? viewingGroupId = null, CancellationToken cancellationToken = default);
    Task RecordCompletedAsync(Guid mediaId, Guid indexedFileId, double duration, Guid? viewingGroupId = null, CancellationToken cancellationToken = default);
    Task RecordSkippedAsync(Guid mediaId, Guid indexedFileId, double position, double duration, Guid? viewingGroupId = null, CancellationToken cancellationToken = default);
    Task RecordRatingAsync(Guid mediaId, int value, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PendingPlaybackEvent>> GetPendingEventsAsync(CancellationToken cancellationToken = default);
    Task MarkSyncedAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken = default);
}

public record PendingPlaybackEvent
{
    public required Guid Id { get; init; }
    public required Guid MediaId { get; init; }
    public required Guid IndexedFileId { get; init; }
    public required PlaybackEventType EventType { get; init; }
    public required double Position { get; init; }
    public required double Duration { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public int? RatingValue { get; init; }
    public Guid? ViewingGroupId { get; init; }
    public bool IsSynced { get; init; }
}

public enum PlaybackEventType
{
    Progress,
    Completed,
    Skipped,
    Rated
}
