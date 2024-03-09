using MediaServer.Application.Common.Comparers;
using MediaServer.Domain.Entities;

namespace MediaServer.Application.Extensions;
public static class MediaFileExtensions
{
    public static
        (IEnumerable<IndexedFile> UnchangedFiles,
        IEnumerable<IndexedFile> AddedFiles,
        IEnumerable<IndexedFile> RemovedFiles,
        IEnumerable<(IndexedFile OldFile, IndexedFile NewFile)> RenamedFiles)
        CompareTo(this IEnumerable<IndexedFile>? oldMediaFiles, IEnumerable<IndexedFile> newMediaFiles)
    {
        if (oldMediaFiles == null)
        {
            return ([], newMediaFiles, [], []);
        }

        var comparer = new IndexedFileComparer();
        var unchanged = oldMediaFiles.Intersect(newMediaFiles, comparer);
        var added = newMediaFiles.Except(oldMediaFiles, comparer).ToList();
        var removed = oldMediaFiles.Except(newMediaFiles, comparer).ToList();
        var renamed = added
            .Join(removed,
                addedFile => new { addedFile.Hash, addedFile.Size },
                removedFile => new { removedFile.Hash, removedFile.Size },
                (addedFile, removedFile) => (NewFile: addedFile, OldFile: removedFile))
            .ToList();

        added.RemoveAll(x => renamed.Any(r => comparer.Equals(x, r.NewFile)));
        removed.RemoveAll(x => renamed.Any(r => comparer.Equals(x, r.OldFile)));

        return (unchanged, added, removed, renamed);
    }
}
