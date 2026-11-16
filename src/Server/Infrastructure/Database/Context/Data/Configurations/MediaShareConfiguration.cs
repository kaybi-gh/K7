using K7.Server.Domain.Entities.Shares;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class MediaShareConfiguration : IEntityTypeConfiguration<MediaShare>
{
    public void Configure(EntityTypeBuilder<MediaShare> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(200);
        builder.Property(x => x.PasswordHash).HasMaxLength(256);

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.CreatedByUserId);
        builder.HasIndex(x => x.ExpiresAt);

        builder.HasOne(x => x.RootMedia)
            .WithMany()
            .HasForeignKey(x => x.RootMediaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(x => x.HasPassword);
    }
}
