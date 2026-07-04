using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class LibraryConfiguration : IEntityTypeConfiguration<Library>
{
    public void Configure(EntityTypeBuilder<Library> builder)
    {
        builder
            .Property(t => t.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder
            .Property(t => t.MetadataProviderName)
            .HasMaxLength(100)
            .IsRequired();

        builder
            .Property(t => t.MetadataLanguage)
            .HasMaxLength(10)
            .IsRequired()
            .HasDefaultValue("fr");

        builder
            .Property(t => t.MetadataFallbackLanguage)
            .HasMaxLength(10)
            .IsRequired()
            .HasDefaultValue("en");

        builder.Property(t => t.IntroDetectionEnabled).HasDefaultValue(true);
        builder.Property(t => t.SeekbarThumbnailGenerationEnabled).HasDefaultValue(true);
        builder.Property(t => t.MusicAudioAnalysisEnabled).HasDefaultValue(true);
        builder.Property(t => t.TranscodingEnabled).HasDefaultValue(true);
        builder.Property(t => t.TransmuxingEnabled).HasDefaultValue(true);
        builder.Property(t => t.RealtimeMonitorEnabled).HasDefaultValue(true);
        builder.Property(t => t.AutoScanIntervalHours).HasDefaultValue(6);

        builder
            .HasMany(l => l.IndexedFiles)
            .WithOne()
            .HasForeignKey(i => i.LibraryId);

        builder
            .HasMany(l => l.ScanIssues)
            .WithOne()
            .HasForeignKey(s => s.LibraryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
