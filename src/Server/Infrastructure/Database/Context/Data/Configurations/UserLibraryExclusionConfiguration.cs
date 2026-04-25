using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class UserLibraryExclusionConfiguration : IEntityTypeConfiguration<UserLibraryExclusion>
{
    public void Configure(EntityTypeBuilder<UserLibraryExclusion> builder)
    {
        builder
            .HasIndex(e => new { e.UserId, e.LibraryId })
            .IsUnique();

        builder.Property(e => e.IsAdminExcluded).HasDefaultValue(false);
        builder.Property(e => e.IsSelfExcluded).HasDefaultValue(false);
    }
}
