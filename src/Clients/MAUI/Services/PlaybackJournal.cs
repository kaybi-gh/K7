using K7.Clients.MAUI.Data;
using K7.Clients.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace K7.Clients.MAUI.Services;

public class PlaybackJournal : IPlaybackJournal
{
    private readonly IDbContextFactory<OfflineMediaDbContext> _dbContextFactory;
    private DateTimeOffset _lastRecordedAt = DateTimeOffset.MinValue;
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromSeconds(10);

    public PlaybackJournal(IDbContextFactory<OfflineMediaDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task RecordProgressAsync(Guid mediaId, Guid indexedFileId, double position, double duration, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastRecordedAt < ThrottleInterval) return;
        _lastRecordedAt = now;

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Save position to downloaded media for resume support
        var downloaded = await db.DownloadedMedia.FirstOrDefaultAsync(d => d.MediaId == mediaId, cancellationToken);
        if (downloaded is not null)
        {
            downloaded.LastPlaybackPosition = position;
            downloaded.LastPlayedAt = now;
        }

        db.PendingPlaybackEvents.Add(new PendingPlaybackEventEntity
        {
            Id = Guid.NewGuid(),
            MediaId = mediaId,
            IndexedFileId = indexedFileId,
            EventType = PlaybackEventType.Progress,
            Position = position,
            Duration = duration,
            Timestamp = now,
            IsSynced = false
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordCompletedAsync(Guid mediaId, Guid indexedFileId, double duration, CancellationToken cancellationToken = default)
    {
        await AddEventAsync(mediaId, indexedFileId, PlaybackEventType.Completed, duration, duration, cancellationToken: cancellationToken);
    }

    public async Task RecordSkippedAsync(Guid mediaId, Guid indexedFileId, double position, double duration, CancellationToken cancellationToken = default)
    {
        await AddEventAsync(mediaId, indexedFileId, PlaybackEventType.Skipped, position, duration, cancellationToken: cancellationToken);
    }

    public async Task RecordRatingAsync(Guid mediaId, int value, CancellationToken cancellationToken = default)
    {
        await AddEventAsync(mediaId, Guid.Empty, PlaybackEventType.Rated, 0, 0, ratingValue: value, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<PendingPlaybackEvent>> GetPendingEventsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.PendingPlaybackEvents
            .Where(e => !e.IsSynced)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new PendingPlaybackEvent
        {
            Id = e.Id,
            MediaId = e.MediaId,
            IndexedFileId = e.IndexedFileId,
            EventType = e.EventType,
            Position = e.Position,
            Duration = e.Duration,
            Timestamp = e.Timestamp,
            RatingValue = e.RatingValue,
            IsSynced = e.IsSynced
        }).ToList();
    }

    public async Task MarkSyncedAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var ids = eventIds.ToList();
        var entities = await db.PendingPlaybackEvents
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(cancellationToken);

        foreach (var entity in entities)
        {
            entity.IsSynced = true;
        }

        await db.SaveChangesAsync(cancellationToken);

        // Purge old synced events (keep last 24h for safety)
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
        var old = await db.PendingPlaybackEvents
            .Where(e => e.IsSynced && e.Timestamp < cutoff)
            .ToListAsync(cancellationToken);

        if (old.Count > 0)
        {
            db.PendingPlaybackEvents.RemoveRange(old);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task AddEventAsync(Guid mediaId, Guid indexedFileId, PlaybackEventType eventType, double position, double duration, int? ratingValue = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.PendingPlaybackEvents.Add(new PendingPlaybackEventEntity
        {
            Id = Guid.NewGuid(),
            MediaId = mediaId,
            IndexedFileId = indexedFileId,
            EventType = eventType,
            Position = position,
            Duration = duration,
            RatingValue = ratingValue,
            Timestamp = DateTimeOffset.UtcNow,
            IsSynced = false
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
