using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class MetadataPictureVariantConfiguration : IEntityTypeConfiguration<MetadataPictureVariant>
{
    public void Configure(EntityTypeBuilder<MetadataPictureVariant> builder)
    {
        builder
            .HasOne(v => v.MetadataPicture)
            .WithMany(p => p.Variants)
            .HasForeignKey(v => v.MetadataPictureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(v => new { v.MetadataPictureId, v.Size })
            .IsUnique();
    }
}
