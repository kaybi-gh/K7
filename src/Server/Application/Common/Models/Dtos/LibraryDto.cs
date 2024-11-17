using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Common.Models.Dtos;

public record LibraryDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required LibraryMediaType MediaType { get; init; }
    public required string RootPath { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<Library, LibraryDto>();
        }
    }
}
