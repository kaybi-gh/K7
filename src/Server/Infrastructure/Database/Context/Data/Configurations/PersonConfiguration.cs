using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder
            .HasMany(p => p.Roles)
            .WithOne(r => r.Person)
            .HasForeignKey(r => r.PersonId);
    }
}
