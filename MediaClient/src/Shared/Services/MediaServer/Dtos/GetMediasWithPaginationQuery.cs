using AutoMapper;
using MediaClient.Shared.Domain.Models;

namespace MediaClient.Shared.Services.MediaServer.Dtos;

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
