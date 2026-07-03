using K7.Server.Domain.Entities.Federation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class PeerSocialAgreementConfiguration : IEntityTypeConfiguration<PeerSocialAgreement>
{
    public void Configure(EntityTypeBuilder<PeerSocialAgreement> builder)
    {
        builder.Property(a => a.ContentType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasIndex(a => new { a.PeerServerId, a.ContentType })
            .IsUnique()
            .HasDatabaseName("IX_PeerSocialAgreements_PeerServerId_ContentType");

        builder.HasOne(a => a.PeerServer)
            .WithMany(p => p.SocialAgreements)
            .HasForeignKey(a => a.PeerServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
