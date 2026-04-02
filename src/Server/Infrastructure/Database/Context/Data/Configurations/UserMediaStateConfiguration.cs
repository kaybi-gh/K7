using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class UserMediaStateConfiguration : IEntityTypeConfiguration<UserMediaState>
{
    public void Configure(EntityTypeBuilder<UserMediaState> builder)
    {
        builder
            .HasIndex(s => new { s.UserId, s.MediaId })
            .IsUnique();

        builder
            .HasIndex(s => new { s.UserId, s.IsCompleted, s.LastInteractedAt })
            .HasDatabaseName("IX_UserMediaStates_UserId_IsCompleted_LastInteractedAt");

        builder
            .HasIndex(s => new { s.UserId, s.LastInteractedAt })
            .HasDatabaseName("IX_UserMediaStates_UserId_LastInteractedAt");

        builder
            .HasIndex(s => new { s.UserId, s.PlayCount })
            .HasDatabaseName("IX_UserMediaStates_UserId_PlayCount");
    }
}
