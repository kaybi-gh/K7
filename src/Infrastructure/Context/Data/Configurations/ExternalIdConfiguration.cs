using MediaServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class ExternalIdConfiguration : IEntityTypeConfiguration<ExternalId>
{
    public void Configure(EntityTypeBuilder<ExternalId> builder)
    {
        builder
            .HasOne(r => r.Metadata)
            .WithMany(m => m.ExternalIds)
            .HasForeignKey(r => r.MetadataId);
    }
}
