using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class IndexedFileConfiguration : IEntityTypeConfiguration<IndexedFile>
{
    public void Configure(EntityTypeBuilder<IndexedFile> builder)
    {
        builder
            .OwnsOne(m => m.Identification);

        builder
            .HasOne(x => x.FileMetadata)
            .WithOne(x => x.IndexedFile)
            .HasForeignKey<BaseFileMetadata>(x => x.IndexedFileId);
    }
}
