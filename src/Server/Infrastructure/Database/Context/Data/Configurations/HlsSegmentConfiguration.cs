using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class HlsSegmentConfiguration : IEntityTypeConfiguration<HlsSegment>
{
    public void Configure(EntityTypeBuilder<HlsSegment> builder)
    {
        builder.HasKey(x => new { x.FileMetadataId, x.Number });
        builder.HasIndex(x => new { x.IndexedFileId, x.Number });
    }
}
