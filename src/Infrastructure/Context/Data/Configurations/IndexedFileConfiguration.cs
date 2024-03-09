using System.Reflection.Emit;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class IndexedFileConfiguration : IEntityTypeConfiguration<IndexedFile>
{
    public void Configure(EntityTypeBuilder<IndexedFile> builder)
    {
    }
}
