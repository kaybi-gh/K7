using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Features.Libraries.Queries.GetLibraries;

public record LibraryDto
{
    public required int Id { get; init; }
    public required string Title { get; set; }
    public required LibraryMediaType MediaType { get; set; }
    public required string RootPath { get; set; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<Library, LibraryDto>();
        }
    }
}
