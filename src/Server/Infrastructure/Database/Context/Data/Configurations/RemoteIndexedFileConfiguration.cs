using K7.Server.Domain.Entities.Federation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class RemoteIndexedFileConfiguration : IEntityTypeConfiguration<RemoteIndexedFile>
{
    public void Configure(EntityTypeBuilder<RemoteIndexedFile> builder)
    {
        builder.Property(f => f.Name)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(f => f.Extension)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(f => f.Media)
            .WithMany(m => m.RemoteIndexedFiles)
            .HasForeignKey(f => f.MediaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Library)
            .WithMany(l => l.RemoteIndexedFiles)
            .HasForeignKey(f => f.LibraryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => new { f.LibraryId, f.Created });
    }
}
