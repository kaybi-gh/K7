using K7.Server.Domain.Entities.Federation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class VisibilityGrantConfiguration : IEntityTypeConfiguration<VisibilityGrant>
{
    public void Configure(EntityTypeBuilder<VisibilityGrant> builder)
    {
        builder.HasOne(g => g.OwnerUser)
            .WithMany()
            .HasForeignKey(g => g.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => g.OwnerUserId)
            .HasDatabaseName("IX_VisibilityGrants_OwnerUserId");
    }
}
