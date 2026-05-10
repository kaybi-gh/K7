using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class LibraryScanIssueConfiguration : IEntityTypeConfiguration<LibraryScanIssue>
{
    public void Configure(EntityTypeBuilder<LibraryScanIssue> builder)
    {
        builder.ToTable("Library_ScanIssues");

        builder.Property(s => s.Path)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(s => s.ErrorMessage)
            .HasMaxLength(2000)
            .IsRequired();

        builder.HasIndex(s => s.LibraryId)
            .HasDatabaseName("IX_Library_ScanIssues_LibraryId");
    }
}
