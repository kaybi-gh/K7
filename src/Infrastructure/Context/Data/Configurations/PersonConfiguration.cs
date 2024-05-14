using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Metadatas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder
            .HasIndex(e => e.Slug)
            .IsUnique();

        builder
            .HasMany(p => p.Roles)
            .WithOne(r => r.Person)
            .HasForeignKey(r => r.PersonId);
    }
}
