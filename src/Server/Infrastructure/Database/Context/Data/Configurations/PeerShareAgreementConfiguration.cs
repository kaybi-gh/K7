using K7.Server.Domain.Entities.Federation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class PeerShareAgreementConfiguration : IEntityTypeConfiguration<PeerShareAgreement>
{
    public void Configure(EntityTypeBuilder<PeerShareAgreement> builder)
    {
        builder.Property(a => a.Direction)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasOne(a => a.Library)
            .WithMany()
            .HasForeignKey(a => a.LibraryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
