using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class UserPlaylistStateConfiguration : IEntityTypeConfiguration<UserPlaylistState>
{
    public void Configure(EntityTypeBuilder<UserPlaylistState> builder)
    {
        builder
            .HasIndex(s => new { s.UserId, s.PlaylistId })
            .IsUnique();

        builder
            .HasIndex(s => new { s.UserId, s.LastListenedAt })
            .HasDatabaseName("IX_UserPlaylistStates_UserId_LastListenedAt");

        builder
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(s => s.Playlist)
            .WithMany(p => p.UserStates)
            .HasForeignKey(s => s.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
