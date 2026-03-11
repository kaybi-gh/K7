using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class MediaPlaybackSessionConfiguration : IEntityTypeConfiguration<MediaPlaybackSession>
{
    public void Configure(EntityTypeBuilder<MediaPlaybackSession> builder)
    {
        builder.HasIndex(e => e.SessionId).IsUnique();
        builder.HasIndex(e => new { e.UserId, e.CompletedAt });
        builder.HasIndex(e => new { e.UserId, e.MediaId, e.CompletedAt });
    }
}
