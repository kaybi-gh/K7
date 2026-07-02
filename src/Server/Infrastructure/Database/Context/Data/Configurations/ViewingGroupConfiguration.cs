using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class ViewingGroupConfiguration : IEntityTypeConfiguration<ViewingGroup>
{
    public void Configure(EntityTypeBuilder<ViewingGroup> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(e => e.HostUserId);
        builder.HasIndex(e => e.CreatedByUserId);

        builder.HasOne(e => e.HostUser)
            .WithMany()
            .HasForeignKey(e => e.HostUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
