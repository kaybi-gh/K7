using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class MediaPlaybackSessionCoViewerConfiguration : IEntityTypeConfiguration<MediaPlaybackSessionCoViewer>
{
    public void Configure(EntityTypeBuilder<MediaPlaybackSessionCoViewer> builder)
    {
        builder.HasIndex(e => new { e.ReferenceId, e.UserId }).IsUnique();
        builder.HasIndex(e => e.UserId);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
