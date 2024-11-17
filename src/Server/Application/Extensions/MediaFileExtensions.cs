using K7.Server.Application.Common.Comparers;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.Extensions;
public static class MediaFileExtensions
{
    public static
        (IEnumerable<IndexedFile> UnchangedFiles,
        IEnumerable<IndexedFile> AddedFiles,
        IEnumerable<IndexedFile> RemovedFiles,
        IEnumerable<(IndexedFile OldFile, IndexedFile NewFile)> RenamedFiles)
        CompareTo(this IEnumerable<IndexedFile>? oldIndexedFiles, IEnumerable<IndexedFile> newIndexedFiles)
    {
        if (oldIndexedFiles == null)
        {
            return ([], newIndexedFiles, [], []);
        }

        var comparer = new IndexedFileComparer();
        var unchanged = oldIndexedFiles.Intersect(newIndexedFiles, comparer);
        var added = newIndexedFiles.Except(oldIndexedFiles, comparer).ToList();
        var removed = oldIndexedFiles.Except(newIndexedFiles, comparer).ToList();
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
