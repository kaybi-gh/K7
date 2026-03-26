using K7.Server.Domain.Entities.Restrictions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class ContentRestrictionProfileConfiguration : IEntityTypeConfiguration<ContentRestrictionProfile>
{
    public void Configure(EntityTypeBuilder<ContentRestrictionProfile> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(2000);

        builder.OwnsMany(p => p.Rules, rules =>
        {
            rules.ToJson();
        });

        builder.HasMany(p => p.Users)
            .WithOne(u => u.ContentRestrictionProfile)
            .HasForeignKey(u => u.ContentRestrictionProfileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
