using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.DisplayName).HasMaxLength(50);

        builder.HasIndex(u => new { u.PeerServerId, u.OriginUserId })
            .IsUnique()
            .HasDatabaseName("IX_Users_PeerServerId_OriginUserId");
    }
}
