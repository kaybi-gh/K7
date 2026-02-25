using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class StreamSessionConfiguration : IEntityTypeConfiguration<StreamSession>
{
    public void Configure(EntityTypeBuilder<StreamSession> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.IndexedFile)
            .WithMany()
            .HasForeignKey(x => x.IndexedFileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Device)
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
