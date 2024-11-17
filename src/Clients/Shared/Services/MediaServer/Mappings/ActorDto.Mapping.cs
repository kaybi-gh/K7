using AutoMapper;
using K7.Clients.Shared.Domain.Models;
using K7.Clients.Shared.Services.MediaServer.Dtos;

namespace K7.Clients.Shared.Services.MediaServer.Mappings;

public class ActorDtoMapping : Profile
{
    public ActorDtoMapping()
    {
        CreateMap<ActorDto, Actor>()
            .ForMember(dst => dst.CharacterName, x => x.MapFrom(src => src.CharacterName));
    }
}
