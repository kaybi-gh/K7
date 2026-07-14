using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class MediaLibraryAvailabilityConfiguration : IEntityTypeConfiguration<MediaLibraryAvailability>
{
    public void Configure(EntityTypeBuilder<MediaLibraryAvailability> builder)
    {
        builder.HasKey(a => new { a.LibraryId, a.MediaId });

        builder.HasIndex(a => a.MediaId);
        builder.HasIndex(a => a.LibraryId);

        builder.HasOne(a => a.Library)
            .WithMany()
            .HasForeignKey(a => a.LibraryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Media)
            .WithMany()
            .HasForeignKey(a => a.MediaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
