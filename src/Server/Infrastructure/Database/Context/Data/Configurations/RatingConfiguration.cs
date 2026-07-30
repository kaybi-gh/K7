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

public class UserRatingConfiguration : IEntityTypeConfiguration<UserRating>
{
    public void Configure(EntityTypeBuilder<UserRating> builder)
    {
        // One local rating per user per media. Without uniqueness, concurrent RateMedia /
        // UpsertMediaReview check-then-insert races create duplicate rows (same class of bug as
        // CreateMedia without an identity lock). MetadataProviderRating rows share the Ratings
        // TPH table with a null UserId and are unaffected by this filtered index.
        builder
            .HasIndex(r => new { r.MediaId, r.UserId })
            .IsUnique()
            .HasDatabaseName("IX_Ratings_MediaId_UserId");
    }
}
