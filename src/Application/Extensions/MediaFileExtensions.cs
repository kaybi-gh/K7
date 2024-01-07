using MediaServer.Domain.Entities.Files;

namespace MediaServer.Application.Extensions;
public static class MediaFileExtensions
{
    public static
        (IEnumerable<Domain.Entities.Files.MediaFile> AddedFiles,
        IEnumerable<Domain.Entities.Files.MediaFile> RemovedFiles,
        IEnumerable<Domain.Entities.Files.MediaFile> UnchangedFiles)
        CompareTo(this IEnumerable<Domain.Entities.Files.MediaFile> oldMediaFiles, IEnumerable<Domain.Entities.Files.MediaFile> newMediaFiles)
    {
        var added = newMediaFiles.Except(oldMediaFiles);
        var removed = oldMediaFiles.Except(newMediaFiles);
        var unchanged = oldMediaFiles.Intersect(newMediaFiles);

        return (added, removed, unchanged);
    }
}
