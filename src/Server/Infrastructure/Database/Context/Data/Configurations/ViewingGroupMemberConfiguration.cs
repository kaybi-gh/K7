using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class ViewingGroupMemberConfiguration : IEntityTypeConfiguration<ViewingGroupMember>
{
    public void Configure(EntityTypeBuilder<ViewingGroupMember> builder)
    {
        builder.HasIndex(e => new { e.ViewingGroupId, e.UserId }).IsUnique();
        builder.HasIndex(e => e.UserId);

        builder.HasOne(e => e.ViewingGroup)
            .WithMany(g => g.Members)
            .HasForeignKey(e => e.ViewingGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
