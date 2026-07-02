using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class MediaPlaybackSessionConfiguration : IEntityTypeConfiguration<MediaPlaybackSession>
{
    public void Configure(EntityTypeBuilder<MediaPlaybackSession> builder)
    {
        builder.HasIndex(e => e.SessionId).IsUnique();
        builder.HasIndex(e => e.ReferenceId);
        builder.HasIndex(e => new { e.UserId, e.CompletedAt });
        builder.HasIndex(e => new { e.UserId, e.StartedAt });
        builder.HasIndex(e => new { e.UserId, e.MediaId, e.CompletedAt });
        builder.HasIndex(e => e.DeviceId);

        builder.HasOne(e => e.Device)
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Details)
            .WithOne(d => d.MediaPlaybackSession)
            .HasForeignKey<PlaybackSessionDetails>(d => d.MediaPlaybackSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.SharedProfileNameSnapshot).HasMaxLength(100);
        builder.Property(e => e.CoWatchingWithSnapshot).HasMaxLength(500);

        builder.HasOne(e => e.SharedProfile)
            .WithMany()
            .HasForeignKey(e => e.SharedProfileId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.SharedProfileId);
    }
}
