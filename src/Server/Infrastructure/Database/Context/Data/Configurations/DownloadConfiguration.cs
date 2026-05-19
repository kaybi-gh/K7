using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class DownloadConfiguration : IEntityTypeConfiguration<Download>
{
    public void Configure(EntityTypeBuilder<Download> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.IndexedFile)
            .WithMany()
            .HasForeignKey(x => x.IndexedFileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Device)
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.OutputPath).HasMaxLength(1024);
        builder.Property(x => x.ContentType).HasMaxLength(100);
        builder.Property(x => x.SubtitleTrackIndices).HasMaxLength(100);
        builder.Property(x => x.FailureReason).HasMaxLength(500);

        builder.HasIndex(x => new { x.IndexedFileId, x.DeviceId, x.UserId });
    }
}
