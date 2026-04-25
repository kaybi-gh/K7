using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class PlaylistConfiguration : IEntityTypeConfiguration<Playlist>
{
    public void Configure(EntityTypeBuilder<Playlist> builder)
    {
        builder
            .Property(p => p.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder
            .Property(p => p.Description)
            .HasMaxLength(2000);

        builder
            .Property(p => p.MediaType)
            .IsRequired()
            .HasDefaultValue(MediaType.MusicTrack);

        builder
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(p => p.Items)
            .WithOne(i => i.Playlist)
            .HasForeignKey(i => i.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
