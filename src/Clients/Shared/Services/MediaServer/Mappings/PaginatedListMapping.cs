using AutoMapper;

namespace K7.Clients.Shared.Services.MediaServer.Mappings;

public class PaginatedListMapping : Profile
{
    public PaginatedListMapping()
    {
        CreateMap(typeof(Dtos.PaginatedList<>), typeof(Domain.Models.PaginatedList<>))
            .ConvertUsing(typeof(PaginatedListConverter<,>));
    }
}
