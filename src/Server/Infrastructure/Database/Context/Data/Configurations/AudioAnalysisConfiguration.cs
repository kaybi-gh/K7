using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class AudioAnalysisConfiguration : IEntityTypeConfiguration<AudioAnalysis>
{
    public void Configure(EntityTypeBuilder<AudioAnalysis> builder)
    {
        // One-to-one: each MusicTrack has at most one AudioAnalysis
        builder.HasIndex(e => e.MusicTrackId).IsUnique();

        builder.HasOne(e => e.MusicTrack)
            .WithOne(t => t.AudioAnalysis)
            .HasForeignKey<AudioAnalysis>(e => e.MusicTrackId)
            .OnDelete(DeleteBehavior.Cascade);

        // Fingerprint can be long, but we don't need to index it - lookups go through AcoustId
        builder.Property(e => e.ChromaprintFingerprint).HasColumnType("text");

        builder.Property(e => e.WaveformPeaks).HasColumnType("jsonb");
    }
}
