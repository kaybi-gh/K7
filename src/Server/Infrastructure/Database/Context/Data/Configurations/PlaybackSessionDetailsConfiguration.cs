using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class PlaybackSessionDetailsConfiguration : IEntityTypeConfiguration<PlaybackSessionDetails>
{
    public void Configure(EntityTypeBuilder<PlaybackSessionDetails> builder)
    {
        builder.HasIndex(e => e.MediaPlaybackSessionId).IsUnique();

        builder.Property(e => e.VideoDecision).HasMaxLength(32);
        builder.Property(e => e.AudioDecision).HasMaxLength(32);
        builder.Property(e => e.SourceVideoCodec).HasMaxLength(32);
        builder.Property(e => e.SourceAudioCodec).HasMaxLength(32);
        builder.Property(e => e.StreamVideoCodec).HasMaxLength(32);
        builder.Property(e => e.StreamAudioCodec).HasMaxLength(32);
    }
}
