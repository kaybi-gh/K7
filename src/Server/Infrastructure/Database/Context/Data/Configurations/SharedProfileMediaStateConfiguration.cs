using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class SharedProfileMediaStateConfiguration : IEntityTypeConfiguration<SharedProfileMediaState>
{
    public void Configure(EntityTypeBuilder<SharedProfileMediaState> builder)
    {
        builder.ToTable("SharedProfileMediaStates");

        builder
            .HasIndex(s => new { s.SharedProfileId, s.MediaId })
            .IsUnique();

        builder
            .HasIndex(s => new { s.SharedProfileId, s.IsCompleted, s.LastInteractedAt })
            .HasDatabaseName("IX_SharedProfileMediaStates_SharedProfileId_IsCompleted_LastInteractedAt");

        builder
            .HasIndex(s => new { s.SharedProfileId, s.LastInteractedAt })
            .HasDatabaseName("IX_SharedProfileMediaStates_SharedProfileId_LastInteractedAt");

        builder
            .HasIndex(s => new { s.SharedProfileId, s.PlayCount })
            .HasDatabaseName("IX_SharedProfileMediaStates_SharedProfileId_PlayCount");

        builder.HasOne(s => s.SharedProfile)
            .WithMany()
            .HasForeignKey(s => s.SharedProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Media)
            .WithMany()
            .HasForeignKey(s => s.MediaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
