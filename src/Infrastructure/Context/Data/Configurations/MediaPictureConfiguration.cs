using MediaServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class MediaPictureConfiguration : IEntityTypeConfiguration<MediaPicture>
{
    public void Configure(EntityTypeBuilder<MediaPicture> builder)
    {
    }
}
