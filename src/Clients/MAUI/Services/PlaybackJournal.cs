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

    public async Task RecordProgressAsync(
        Guid mediaId,
        Guid indexedFileId,
        double position,
        double duration,
        string identityUserId,
        Guid? sharedProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastRecordedAt < ThrottleInterval)
            return;
        _lastRecordedAt = now;

        await using var db = await OpenAsync(cancellationToken);

        var downloaded = await db.DownloadedMedia.FirstOrDefaultAsync(d => d.MediaId == mediaId, cancellationToken);
        if (downloaded is not null)
        {
            downloaded.LastPlaybackPosition = position;
            downloaded.LastPlayedAt = now;
        }

        if (!string.IsNullOrEmpty(identityUserId))
        {
            db.PendingPlaybackEvents.Add(CreateEvent(
                mediaId,
                indexedFileId,
                PlaybackEventType.Progress,
                position,
                duration,
                identityUserId,
                sharedProfileId,
                ratingValue: null,
                now));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task RecordCompletedAsync(
        Guid mediaId,
        Guid indexedFileId,
        double duration,
        string identityUserId,
        Guid? sharedProfileId = null,
        CancellationToken cancellationToken = default) =>
        AddEventAsync(
            mediaId,
            indexedFileId,
            PlaybackEventType.Completed,
            duration,
            duration,
            identityUserId,
            sharedProfileId,
            cancellationToken: cancellationToken);

    public Task RecordSkippedAsync(
        Guid mediaId,
        Guid indexedFileId,
        double position,
        double duration,
        string identityUserId,
        Guid? sharedProfileId = null,
        CancellationToken cancellationToken = default) =>
        AddEventAsync(
            mediaId,
            indexedFileId,
            PlaybackEventType.Skipped,
            position,
            duration,
            identityUserId,
            sharedProfileId,
            cancellationToken: cancellationToken);

    public Task RecordRatingAsync(
        Guid mediaId,
        int value,
        string identityUserId,
        CancellationToken cancellationToken = default) =>
        AddEventAsync(
            mediaId,
            Guid.Empty,
            PlaybackEventType.Rated,
            0,
            0,
            identityUserId,
            sharedProfileId: null,
            ratingValue: value,
            cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<PendingPlaybackEvent>> GetPendingEventsAsync(
        string identityUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(identityUserId))
            return [];

        await using var db = await OpenAsync(cancellationToken);
        var entities = await db.PendingPlaybackEvents
            .Where(e => !e.IsSynced && e.IdentityUserId == identityUserId)
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
            IdentityUserId = e.IdentityUserId,
            RatingValue = e.RatingValue,
            SharedProfileId = e.SharedProfileId,
            IsSynced = e.IsSynced
        }).ToList();
    }

    public async Task MarkSyncedAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken = default)
    {
        await using var db = await OpenAsync(cancellationToken);
        var ids = eventIds.ToList();
        var entities = await db.PendingPlaybackEvents
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(cancellationToken);

        foreach (var entity in entities)
            entity.IsSynced = true;

        await db.SaveChangesAsync(cancellationToken);

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

    private async Task AddEventAsync(
        Guid mediaId,
        Guid indexedFileId,
        PlaybackEventType eventType,
        double position,
        double duration,
        string identityUserId,
        Guid? sharedProfileId,
        int? ratingValue = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(identityUserId))
            return;

        await using var db = await OpenAsync(cancellationToken);
        db.PendingPlaybackEvents.Add(CreateEvent(
            mediaId,
            indexedFileId,
            eventType,
            position,
            duration,
            identityUserId,
            sharedProfileId,
            ratingValue,
            DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static PendingPlaybackEventEntity CreateEvent(
        Guid mediaId,
        Guid indexedFileId,
        PlaybackEventType eventType,
        double position,
        double duration,
        string identityUserId,
        Guid? sharedProfileId,
        int? ratingValue,
        DateTimeOffset timestamp) =>
        new()
        {
            Id = Guid.NewGuid(),
            MediaId = mediaId,
            IndexedFileId = indexedFileId,
            EventType = eventType,
            Position = position,
            Duration = duration,
            RatingValue = ratingValue,
            SharedProfileId = sharedProfileId,
            IdentityUserId = identityUserId,
            Timestamp = timestamp,
            IsSynced = false
        };

    private async Task<OfflineMediaDbContext> OpenAsync(CancellationToken cancellationToken)
    {
        await OfflineDbBootstrap.Ready.WaitAsync(cancellationToken);
        return await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    }
}
