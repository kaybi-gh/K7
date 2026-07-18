using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class SyncPlayInviteConfiguration : IEntityTypeConfiguration<SyncPlayInvite>
{
    public void Configure(EntityTypeBuilder<SyncPlayInvite> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Token)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(x => x.Token)
            .IsUnique();

        builder.Property(x => x.CreatedByUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.HasIndex(x => x.GroupId);
        builder.HasIndex(x => x.CreatedAt);
    }
}
