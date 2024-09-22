using MediaServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class HlsSegmentConfiguration : IEntityTypeConfiguration<HlsSegment>
{
    public void Configure(EntityTypeBuilder<HlsSegment> builder)
    {
        builder.HasKey(x => new { x.FileMetadataId, x.Number });
        builder.HasIndex(x => new { x.IndexedFileId, x.Number });
    }
}
