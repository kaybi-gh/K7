using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class MediaSegmentConfiguration : IEntityTypeConfiguration<MediaSegment>
{
    public void Configure(EntityTypeBuilder<MediaSegment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.MediaId);
        builder.HasIndex(x => new { x.MediaId, x.Type });

        builder.HasOne(x => x.Media)
            .WithMany(x => x.Segments)
            .HasForeignKey(x => x.MediaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
