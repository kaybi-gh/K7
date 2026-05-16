using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class PersonRoleConfiguration : IEntityTypeConfiguration<BasePersonRole>
{
    public void Configure(EntityTypeBuilder<BasePersonRole> builder)
    {
        builder
            .HasDiscriminator(m => m.Type)
            .HasValue<Actor>(PersonRoleType.Actor)
            .HasValue<MusicArtistMember>(PersonRoleType.MusicArtist)
            .HasValue<VoiceActor>(PersonRoleType.VoiceActor)
            .HasValue<CrewMember>(PersonRoleType.CrewMember);
    }

    public static void Configure(EntityTypeBuilder<Actor> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<MusicArtistMember> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<VoiceActor> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<CrewMember> builder)
    {
    }
}
