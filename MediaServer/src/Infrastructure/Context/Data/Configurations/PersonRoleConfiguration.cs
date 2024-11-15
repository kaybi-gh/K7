using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Metadatas.PersonRoles;
using MediaServer.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class PersonRoleConfiguration : IEntityTypeConfiguration<BasePersonRole>
{
    public void Configure(EntityTypeBuilder<BasePersonRole> builder)
    {
        builder
            .HasDiscriminator(m => m.Type)
            .HasValue<Actor>(PersonRoleType.Actor)
            .HasValue<MusicArtist>(PersonRoleType.MusicArtist)
            .HasValue<VoiceActor>(PersonRoleType.VoiceActor)
            .HasValue<CrewMember>(PersonRoleType.CrewMember);
    }

    public static void Configure(EntityTypeBuilder<Actor> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<MusicArtist> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<VoiceActor> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<CrewMember> builder)
    {
    }
}
