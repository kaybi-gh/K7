using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class PlaybackBookmarkConfiguration : IEntityTypeConfiguration<PlaybackBookmark>
{
    public void Configure(EntityTypeBuilder<PlaybackBookmark> builder)
    {
        builder.ToTable("PlaybackBookmarks");

        builder
            .HasDiscriminator(b => b.Kind)
            .HasValue<ItemPlaybackBookmark>(PlaybackBookmarkKind.Item)
            .HasValue<SeriesPlaybackBookmark>(PlaybackBookmarkKind.Series);

        builder.HasIndex(b => b.UserId);
        builder.HasIndex(b => b.SharedProfileId);
        builder.HasIndex(b => new { b.UserId, b.Kind, b.UpdatedAt });
        builder.HasIndex(b => new { b.SharedProfileId, b.Kind, b.UpdatedAt });

        builder.HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.SharedProfile)
            .WithMany()
            .HasForeignKey(b => b.SharedProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ItemPlaybackBookmarkConfiguration : IEntityTypeConfiguration<ItemPlaybackBookmark>
{
    public void Configure(EntityTypeBuilder<ItemPlaybackBookmark> builder)
    {
        builder.Property(b => b.MediaId).IsRequired();

        builder.HasOne(b => b.Media)
            .WithMany()
            .HasForeignKey(b => b.MediaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => b.MediaId)
            .HasDatabaseName("IX_PlaybackBookmarks_MediaId_Item");

        builder
            .HasIndex(b => new { b.UserId, b.MediaId })
            .IsUnique()
            .HasDatabaseName("IX_PlaybackBookmarks_UserId_MediaId_Item");

        builder
            .HasIndex(b => new { b.SharedProfileId, b.MediaId })
            .IsUnique()
            .HasDatabaseName("IX_PlaybackBookmarks_SharedProfileId_MediaId_Item");
    }
}

public class SeriesPlaybackBookmarkConfiguration : IEntityTypeConfiguration<SeriesPlaybackBookmark>
{
    public void Configure(EntityTypeBuilder<SeriesPlaybackBookmark> builder)
    {
        builder.Property(b => b.SerieId).IsRequired();
        builder.Property(b => b.LastCompletedEpisodeId).IsRequired();

        builder.HasOne(b => b.Serie)
            .WithMany()
            .HasForeignKey(b => b.SerieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.LastCompletedEpisode)
            .WithMany()
            .HasForeignKey(b => b.LastCompletedEpisodeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.NextEpisode)
            .WithMany()
            .HasForeignKey(b => b.NextEpisodeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(b => b.SerieId)
            .HasDatabaseName("IX_PlaybackBookmarks_SerieId_Series");

        builder.HasIndex(b => b.NextEpisodeId)
            .HasDatabaseName("IX_PlaybackBookmarks_NextEpisodeId_Series");

        builder
            .HasIndex(b => new { b.UserId, b.SerieId })
            .IsUnique()
            .HasDatabaseName("IX_PlaybackBookmarks_UserId_SerieId_Series");

        builder
            .HasIndex(b => new { b.SharedProfileId, b.SerieId })
            .IsUnique()
            .HasDatabaseName("IX_PlaybackBookmarks_SharedProfileId_SerieId_Series");

        builder.HasIndex(b => new { b.UserId, b.ActivityAt });
        builder.HasIndex(b => new { b.SharedProfileId, b.ActivityAt });
        builder.HasIndex(b => new { b.UserId, b.NextEpisodeAvailableAt });
        builder.HasIndex(b => new { b.SharedProfileId, b.NextEpisodeAvailableAt });
    }
}
