using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class ExternalIdConfiguration : IEntityTypeConfiguration<ExternalId>
{
    public void Configure(EntityTypeBuilder<ExternalId> builder)
    {
        builder
            .HasOne(e => e.Media)
            .WithMany(m => m.ExternalIds)
            .HasForeignKey(e => e.MediaId)
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
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasIndex(e => new { e.ProviderName, e.Value });
    }
}
