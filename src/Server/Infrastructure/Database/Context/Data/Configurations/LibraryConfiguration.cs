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
            .HasMany(l => l.IndexedFiles)
            .WithOne()
            .HasForeignKey(i => i.LibraryId);
    }
}
