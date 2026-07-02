using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class SharedProfileMemberConfiguration : IEntityTypeConfiguration<SharedProfileMember>
{
    public void Configure(EntityTypeBuilder<SharedProfileMember> builder)
    {
        builder.ToTable("SharedProfileMembers");

        builder.HasIndex(e => new { e.SharedProfileId, e.UserId }).IsUnique();
        builder.HasIndex(e => e.UserId);

        builder.HasOne(e => e.SharedProfile)
            .WithMany(g => g.Members)
            .HasForeignKey(e => e.SharedProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
