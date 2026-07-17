using K7.Server.Domain.Entities.SharedProfiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class SharedProfilePlaylistConfiguration : IEntityTypeConfiguration<SharedProfilePlaylist>
{
    public void Configure(EntityTypeBuilder<SharedProfilePlaylist> builder)
    {
        builder.ToTable("SharedProfilePlaylists");

        builder
            .HasIndex(s => new { s.SharedProfileId, s.PlaylistId })
            .IsUnique();

        builder.HasOne(s => s.SharedProfile)
            .WithMany()
            .HasForeignKey(s => s.SharedProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Playlist)
            .WithMany()
            .HasForeignKey(s => s.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
