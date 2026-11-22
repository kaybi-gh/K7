using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class UserGuardianConfiguration : IEntityTypeConfiguration<UserGuardian>
{
    public void Configure(EntityTypeBuilder<UserGuardian> builder)
    {
        builder.ToTable("UserGuardians");

        builder.HasIndex(e => new { e.UserId, e.GuardianUserId }).IsUnique();
        builder.HasIndex(e => e.GuardianUserId);

        builder.HasOne(e => e.User)
            .WithMany(u => u.Guardians)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.GuardianUser)
            .WithMany()
            .HasForeignKey(e => e.GuardianUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
