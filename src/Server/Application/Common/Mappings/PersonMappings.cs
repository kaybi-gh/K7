using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.PersonRoles;
using K7.Shared.Dtos.Entities.Persons;

namespace K7.Server.Application.Common.Mappings;

public static class PersonMappings
{
    extension(Person domain)
    {
        public PersonDto ToPersonDto() => new()
        {
            Id = domain.Id,
            Name = domain.Name,
            Gender = domain.Gender,
            Biography = domain.Biography,
            Birthday = domain.Birthday,
            Deathday = domain.Deathday,
            BirthPlace = domain.BirthPlace,
            Roles = domain.Roles.Select(r => r.ToPersonRoleDto()).ToList(),
            ExternalIds = domain.ExternalIds.Select(e => e.ToExternalIdDto()).ToList(),
            PortraitPicture = domain.PortraitPicture?.ToMetadataPictureDto()
        };

        public LitePersonDto ToLitePersonDto() => new()
        {
            Id = domain.Id,
            Name = domain.Name,
            Gender = domain.Gender,
            Birthday = domain.Birthday,
            Deathday = domain.Deathday,
            BirthPlace = domain.BirthPlace,
            PortraitPicture = domain.PortraitPicture?.ToMetadataPictureDto()
        };
    }

    extension(BasePersonRole domain)
    {
        public PersonRoleDto ToPersonRoleDto() => domain switch
        {
            Actor actor => new ActorDto()
            {
                Id = domain.Id,
                MediaId = domain.MediaId,
                Order = domain.Order,
                PortraitPicture = domain.PortraitPicture?.ToMetadataPictureDto(),
                Media = domain.Media.ToLiteMediaDto(),
                Person = domain.Person.ToLitePersonDto(),
                ExternalIds = domain.ExternalIds.Select(e => e.ToExternalIdDto()).ToList(),
                CharacterName = actor.CharacterName
            },
            CrewMember crewMember => new CrewMemberDto()
            {
                Id = domain.Id,
                MediaId = domain.MediaId,
                Order = domain.Order,
                PortraitPicture = domain.PortraitPicture?.ToMetadataPictureDto(),
                Media = domain.Media.ToLiteMediaDto(),
                Person = domain.Person.ToLitePersonDto(),
                ExternalIds = domain.ExternalIds.Select(e => e.ToExternalIdDto()).ToList(),
                Department = crewMember.Department,
                Job = crewMember.Department
            },
            MusicArtist musicArtist => new MusicArtistRoleDto()
            {
                Id = domain.Id,
                MediaId = domain.MediaId,
                Order = domain.Order,
                PortraitPicture = domain.PortraitPicture?.ToMetadataPictureDto(),
                Media = domain.Media.ToLiteMediaDto(),
                Person = domain.Person.ToLitePersonDto(),
                ExternalIds = domain.ExternalIds.Select(e => e.ToExternalIdDto()).ToList(),
                IsGuest = musicArtist.IsGuest
            },
            _ => throw new NotSupportedException($"Unknown type: {domain.GetType().Name}")
        };

        public LitePersonRoleDto ToLitePersonRoleDto() => domain switch
        {
            Actor actor => new LiteActorDto()
            {
                Id = domain.Id,
                MediaId = domain.MediaId,
                Order = domain.Order,
                PortraitPicture = domain.PortraitPicture?.ToMetadataPictureDto(),
                Person = domain.Person.ToLitePersonDto(),
                CharacterName = actor.CharacterName
            },
            CrewMember crewMember => new LiteCrewMemberDto()
            {
                Id = domain.Id,
                MediaId = domain.MediaId,
                Order = domain.Order,
                PortraitPicture = domain.PortraitPicture?.ToMetadataPictureDto(),
                Person = domain.Person.ToLitePersonDto(),
                Department = crewMember.Department,
                Job = crewMember.Department
            },
            MusicArtist musicArtist => new LiteMusicArtistRoleDto()
            {
                Id = domain.Id,
                MediaId = domain.MediaId,
                Order = domain.Order,
                PortraitPicture = domain.PortraitPicture?.ToMetadataPictureDto(),
                Person = domain.Person.ToLitePersonDto(),
                IsGuest = musicArtist.IsGuest
            },
            _ => throw new NotSupportedException($"Unknown type: {domain.GetType().Name}")
        };
    }
}
