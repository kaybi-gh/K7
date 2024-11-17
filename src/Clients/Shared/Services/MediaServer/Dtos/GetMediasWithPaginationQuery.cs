using AutoMapper;
using K7.Clients.Shared.Domain.Models;

namespace K7.Clients.Shared.Services.MediaServer.Dtos;

public record GetMediasWithPaginationQuery
{
    public int? LibraryId { get; init; }
    public MediaType? MediaType { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<GetLiteMediasQuery, GetMediasWithPaginationQuery>();
        }
    }
}
