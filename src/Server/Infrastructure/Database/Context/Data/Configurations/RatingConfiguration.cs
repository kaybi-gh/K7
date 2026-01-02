using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class RatingConfiguration : IEntityTypeConfiguration<BaseRating>
{
    public void Configure(EntityTypeBuilder<BaseRating> builder)
    {
        builder
            .HasDiscriminator(m => m.Source)
            .HasValue<MetadataProviderRating>(RatingSource.MetadataProvider)
            .HasValue<UserRating>(RatingSource.LocalUser);

        builder
            .HasOne(r => r.Media)
            .WithMany(m => m.Ratings)
            .HasForeignKey(r => r.MediaId);
    }
    public void Configure(EntityTypeBuilder<UserRating> builder)
    {
        builder
            .HasOne(r => r.User)
            .WithMany(u => u.Ratings)
            .HasForeignKey(r => r.UserId);
    }
}
