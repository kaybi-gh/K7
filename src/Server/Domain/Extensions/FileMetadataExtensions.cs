using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;

namespace K7.Server.Domain.Extensions;

public static class FileMetadataExtensions
{
    public static IList<HlsSegment> GetHlsSegments(this BaseFileMetadata metadata) => metadata switch
    {
        AudioFileMetadata audio => audio.HlsSegments,
        VideoFileMetadata video => video.HlsSegments,
        _ => []
    };
}
