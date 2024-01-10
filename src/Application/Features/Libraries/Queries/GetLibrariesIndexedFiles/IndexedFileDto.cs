using MediaServer.Domain.Entities;

namespace MediaServer.Application.Features.Libraries.Queries.GetLibrariesFiles;

public record IndexedFileDto
{
    public required int LibraryId { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required string Path { get; init; }
    public string? ParentDirectory { get; init; }
    public required string Hash { get; init; }
    public required long Size { get; init; }
    public bool IsSplitPart { get; init; }
    public bool IsComposite { get; init; }
    public bool IsIdentified { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<IndexedFile, IndexedFileDto>();
        }
    }
}
