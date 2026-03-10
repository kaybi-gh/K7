using K7.Server.Domain.Entities.Playlists;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class PlaylistItemConfiguration : IEntityTypeConfiguration<PlaylistItem>
{
    public void Configure(EntityTypeBuilder<PlaylistItem> builder)
    {
        builder
            .HasOne(i => i.Media)
            .WithMany()
            .HasForeignKey(i => i.MediaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.PlaylistId, i.Order });
    }
}
