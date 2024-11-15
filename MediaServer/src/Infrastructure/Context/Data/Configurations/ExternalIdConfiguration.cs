using MediaServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class ExternalIdConfiguration : IEntityTypeConfiguration<ExternalId>
{
    public void Configure(EntityTypeBuilder<ExternalId> builder)
    {
        builder
            .HasOne(e => e.Metadata)
            .WithMany(m => m.ExternalIds)
            .HasForeignKey(e => e.MetadataId)
            .IsRequired(false);

        builder
            .HasOne(e => e.Person)
            .WithMany(p => p.ExternalIds)
            .HasForeignKey(e => e.PersonId)
            .IsRequired(false);

        builder
            .HasOne(e => e.PersonRole)
            .WithMany(pr => pr.ExternalIds)
            .HasForeignKey(e => e.PersonRoleId)
            .IsRequired(false);
    }
}
