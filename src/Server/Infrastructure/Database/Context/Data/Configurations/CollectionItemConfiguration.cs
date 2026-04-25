using K7.Server.Domain.Entities.Collections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class CollectionItemConfiguration : IEntityTypeConfiguration<CollectionItem>
{
    public void Configure(EntityTypeBuilder<CollectionItem> builder)
    {
        builder
            .HasOne(i => i.Media)
            .WithMany()
            .HasForeignKey(i => i.MediaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
