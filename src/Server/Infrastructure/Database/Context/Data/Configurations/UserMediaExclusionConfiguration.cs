using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class UserMediaExclusionConfiguration : IEntityTypeConfiguration<UserMediaExclusion>
{
    public void Configure(EntityTypeBuilder<UserMediaExclusion> builder)
    {
        builder
            .HasIndex(e => new { e.UserId, e.MediaId })
            .IsUnique();
    }
}
