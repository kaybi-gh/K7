using K7.Server.Domain.Entities;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetIndexedFiles;

public record IndexedFileDto
{
    public required Guid LibraryId { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required string Path { get; init; }
    public string? ParentDirectory { get; init; }
    public required string Hash { get; init; }
    public required long Size { get; init; }
    public bool IsSplitPart { get; init; }
    public bool IsComposite { get; init; }
    public Uri? DirectStreamUri { get; init; }
    public Uri? HlsStreamUri { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<IndexedFile, IndexedFileDto>()
                .ForMember(dest => dest.DirectStreamUri, x => x.MapFrom(src => new Uri($"/api/indexed-files/{src.Id}/direct-stream", UriKind.Relative)))
                .ForMember(dest => dest.HlsStreamUri, x => x.MapFrom(src => new Uri($"/api/indexed-files/{src.Id}/hls-stream/manifest.m3u8", UriKind.Relative)));
        }
    }
}
