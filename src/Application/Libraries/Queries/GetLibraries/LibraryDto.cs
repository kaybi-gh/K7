using MediaServer.Domain.Entities;

namespace MediaServer.Application.Libraries.Queries.GetLibraries;

public record LibraryDto
{
    public int Id { get; init; }
    public string? Title { get; init; }
    public string? RootPath { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<Library, LibraryDto>();
        }
    }
}
