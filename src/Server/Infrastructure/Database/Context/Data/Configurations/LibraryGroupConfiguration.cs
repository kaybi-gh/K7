using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class LibraryGroupConfiguration : IEntityTypeConfiguration<LibraryGroup>
{
    public void Configure(EntityTypeBuilder<LibraryGroup> builder)
    {
        builder
            .Property(g => g.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder
            .Property(g => g.Icon)
            .HasMaxLength(100);

        builder
            .Property(g => g.Description)
            .HasMaxLength(1000);

        builder
            .HasMany(g => g.Libraries)
            .WithOne(l => l.LibraryGroup)
            .HasForeignKey(l => l.LibraryGroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
