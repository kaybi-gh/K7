using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class MediaRecommendationConfiguration : IEntityTypeConfiguration<MediaRecommendation>
{
    public void Configure(EntityTypeBuilder<MediaRecommendation> builder)
    {
        builder.HasKey(r => new { r.MediaId, r.ProviderName });

        builder.Property(r => r.ProviderName).HasMaxLength(50);
    }
}
