using AutoMapper;
using MediaClient.Shared.Domain.Models;
using MediaClient.Shared.Services.MediaServer.Dtos;

namespace MediaClient.Shared.Services.MediaServer.Mappings;

public class ActorDtoMapping : Profile
{
    public ActorDtoMapping()
    {
        CreateMap<ActorDto, Actor>()
            .ForMember(dst => dst.CharacterName, x => x.MapFrom(src => src.CharacterName));
    }
}
