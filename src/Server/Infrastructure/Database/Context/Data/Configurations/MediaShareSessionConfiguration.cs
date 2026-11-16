using K7.Server.Domain.Entities.Shares;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class MediaShareSessionConfiguration : IEntityTypeConfiguration<MediaShareSession>
{
    public void Configure(EntityTypeBuilder<MediaShareSession> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.ExpiresAt);

        builder.HasOne(x => x.Share)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.ShareId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Device)
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
