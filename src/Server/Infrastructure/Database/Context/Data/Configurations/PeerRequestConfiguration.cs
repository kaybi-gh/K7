using K7.Server.Domain.Entities.Federation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class PeerRequestConfiguration : IEntityTypeConfiguration<PeerRequest>
{
    public void Configure(EntityTypeBuilder<PeerRequest> builder)
    {
        builder.Property(r => r.RequesterUrl)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.RequesterName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.Token)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(50);
    }
}
