using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace K7.Clients.MAUI.Data;

public class OfflineMediaDbContext : DbContext
{
    public DbSet<DownloadedMediaEntity> DownloadedMedia => Set<DownloadedMediaEntity>();
    public DbSet<PendingPlaybackEventEntity> PendingPlaybackEvents => Set<PendingPlaybackEventEntity>();

    public OfflineMediaDbContext(DbContextOptions<OfflineMediaDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DownloadedMediaEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IndexedFileId).IsUnique();
            entity.HasIndex(e => e.MediaType);
            entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Artist).HasMaxLength(300);
            entity.Property(e => e.AlbumTitle).HasMaxLength(300);
            entity.Property(e => e.MediaLocalPath).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.CoverLocalPath).HasMaxLength(1024);
            entity.Property(e => e.SubtitleLocalPathsJson).HasMaxLength(2048);
        });

        modelBuilder.Entity<PendingPlaybackEventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IsSynced);
            entity.HasIndex(e => e.Timestamp);
        });

        // SQLite does not support DateTimeOffset in ORDER BY. Store as sortable TEXT.
        var converter = new ValueConverter<DateTimeOffset, string>(
            v => v.ToString("O"),
            v => DateTimeOffset.Parse(v));
        var nullableConverter = new ValueConverter<DateTimeOffset?, string?>(
            v => v.HasValue ? v.Value.ToString("O") : null,
            v => v != null ? DateTimeOffset.Parse(v) : null);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                    property.SetValueConverter(converter);
                else if (property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(nullableConverter);
            }
        }
    }
}

public class DownloadedMediaEntity
{
    public Guid Id { get; set; }
    public Guid IndexedFileId { get; set; }
    public Guid MediaId { get; set; }
    public MediaType MediaType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Artist { get; set; }
    public string? AlbumTitle { get; set; }
    public string? CoverLocalPath { get; set; }
    public string MediaLocalPath { get; set; } = string.Empty;
    public string? SubtitleLocalPathsJson { get; set; }
    public long FileSize { get; set; }
    public DateTimeOffset DownloadedAt { get; set; }
    public DateTimeOffset? LastPlayedAt { get; set; }
    public double LastPlaybackPosition { get; set; }
    public bool IsCacheItem { get; set; }
}

public class PendingPlaybackEventEntity
{
    public Guid Id { get; set; }
    public Guid MediaId { get; set; }
    public Guid IndexedFileId { get; set; }
    public PlaybackEventType EventType { get; set; }
    public double Position { get; set; }
    public double Duration { get; set; }
    public int? RatingValue { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public bool IsSynced { get; set; }
}
