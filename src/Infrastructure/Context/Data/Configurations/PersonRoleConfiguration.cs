using MediaServer.Domain.Entities.Metadatas.Persons;
using MediaServer.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class PersonRoleConfiguration : IEntityTypeConfiguration<BasePersonRole>
{
    public void Configure(EntityTypeBuilder<BasePersonRole> builder)
    {
        builder
            .HasDiscriminator(m => m.Job)
            .HasValue<Actor>(PersonJob.Actor)
            .HasValue<MusicArtist>(PersonJob.MusicArtist)
            .HasValue<VoiceActor>(PersonJob.VoiceActor);

        builder
            .HasOne(r => r.Person)
            .WithMany(p => p.Roles);

        builder
            .HasOne(r => r.Metadata)
            .WithMany(m => m.PersonRoles);
    }
}
