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
