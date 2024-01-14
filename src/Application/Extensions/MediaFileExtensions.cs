using MediaServer.Domain.Entities;

namespace MediaServer.Application.Extensions;
public static class MediaFileExtensions
{
    public static
        (IEnumerable<IndexedFile> UnchangedFiles,
        IEnumerable<IndexedFile> AddedFiles,
        IEnumerable<IndexedFile> RemovedFiles)
        CompareTo(this IEnumerable<IndexedFile> oldMediaFiles, IEnumerable<IndexedFile> newMediaFiles)
    {
        // TODO - Create custom comparer using Hash + Filename + Size
        var unchanged = oldMediaFiles.Intersect(newMediaFiles);
        var added = newMediaFiles.Except(oldMediaFiles);
        var removed = oldMediaFiles.Except(newMediaFiles);

        return (unchanged, added, removed);
    }
}
