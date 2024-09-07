using MediaServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class HlsSegmentConfiguration : IEntityTypeConfiguration<HlsSegment>
{
    public void Configure(EntityTypeBuilder<HlsSegment> builder)
    {
        builder.HasIndex(x => x.VideoFileMetadataId);
        builder.HasIndex(x => x.SegmentId);
    }
}
