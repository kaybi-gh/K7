using K7.Server.Domain.Entities.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class MediaReviewConfiguration : IEntityTypeConfiguration<MediaReview>
{
    public void Configure(EntityTypeBuilder<MediaReview> builder)
    {
        builder.Property(r => r.Text)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(r => r.Emoji)
            .HasMaxLength(16);

        builder.HasIndex(r => new { r.MediaId, r.UserId })
            .IsUnique()
            .HasDatabaseName("IX_MediaReviews_MediaId_UserId");

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Media)
            .WithMany()
            .HasForeignKey(r => r.MediaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.UserRating)
            .WithOne()
            .HasForeignKey<MediaReview>(r => r.UserRatingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
