using AutoMapper;
using K7.Clients.Shared.Services.MediaServer.Mappings;

namespace K7.Clients.Shared.Services.MediaServer.Dtos;

public record PaginatedList<T>
{
    public IReadOnlyCollection<T> Items { get; init; } = [];
    public int PageNumber { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public bool HasPreviousPage { get; init; }
    public bool HasNextPage { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap(typeof(Dtos.PaginatedList<>), typeof(Domain.Models.PaginatedList<>))
                .ConvertUsing(typeof(PaginatedListConverter<,>));
        }
    }
}
