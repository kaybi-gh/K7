using MediaServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class MetadataPictureConfiguration : IEntityTypeConfiguration<MetadataPicture>
{
    public void Configure(EntityTypeBuilder<MetadataPicture> builder)
    {
        builder
            .HasOne(r => r.Metadata)
            .WithMany(m => m.Pictures)
            .HasForeignKey(r => r.MetadataId);
    }
}
