using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class SharedProfileConfiguration : IEntityTypeConfiguration<SharedProfile>
{
    public void Configure(EntityTypeBuilder<SharedProfile> builder)
    {
        builder.ToTable("SharedProfiles");

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

        builder.HasOne(e => e.ContentRestrictionProfile)
            .WithMany()
            .HasForeignKey(e => e.ContentRestrictionProfileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
