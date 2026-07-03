using K7.Server.Domain.Entities.Federation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class PeerServerConfiguration : IEntityTypeConfiguration<PeerServer>
{
    public void Configure(EntityTypeBuilder<PeerServer> builder)
    {
        builder.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.BaseUrl)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.OutboundClientId)
            .HasMaxLength(200);

        builder.Property(p => p.OutboundClientSecret)
            .HasMaxLength(500);

        builder.Property(p => p.InboundApplicationId)
            .HasMaxLength(200);

        builder.Property(p => p.FederationAssertionSecret)
            .HasMaxLength(256);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasMany(p => p.ShareAgreements)
            .WithOne(a => a.PeerServer)
            .HasForeignKey(a => a.PeerServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.RemoteLibraries)
            .WithOne(l => l.PeerServer)
            .HasForeignKey(l => l.PeerServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.RemoteIndexedFiles)
            .WithOne(f => f.PeerServer)
            .HasForeignKey(f => f.PeerServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.SocialAgreements)
            .WithOne(a => a.PeerServer)
            .HasForeignKey(a => a.PeerServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
