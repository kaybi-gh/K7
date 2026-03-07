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
        CompareTo(this IEnumerable<IndexedFile>? oldIndexedFiles, IEnumerable<IndexedFile> newIndexedFiles, HashSet<string>? skippedFilePaths = null)
    {
        if (oldIndexedFiles == null)
        {
            return ([], newIndexedFiles, [], []);
        }

        var comparer = new IndexedFileComparer();

        var oldFilesDict = new Dictionary<IndexedFile, IndexedFile>(comparer);
        foreach (var file in oldIndexedFiles)
        {
            oldFilesDict.TryAdd(file, file);
        }

        List<IndexedFile> unchanged = [];
        List<IndexedFile> added = [];
        var matchedOld = new HashSet<IndexedFile>(comparer);

        foreach (var newFile in newIndexedFiles)
        {
            if (oldFilesDict.TryGetValue(newFile, out var oldFile))
            {
                unchanged.Add(oldFile);
                matchedOld.Add(oldFile);
            }
            else
            {
                added.Add(newFile);
            }
        }

        var removed = oldFilesDict.Values
            .Where(f => !matchedOld.Contains(f))
            .Where(f => skippedFilePaths == null || !skippedFilePaths.Contains(f.Path))
            .ToList();

        var removedByHashSize = removed.ToLookup(f => (f.Hash, f.Size));
        List<(IndexedFile NewFile, IndexedFile OldFile)> renamed = [];
        var renamedAdded = new HashSet<IndexedFile>();
        var renamedRemoved = new HashSet<IndexedFile>();

        foreach (var addedFile in added)
        {
            var match = removedByHashSize[(addedFile.Hash, addedFile.Size)]
                .FirstOrDefault(c => !renamedRemoved.Contains(c));
            if (match != null)
            {
                renamed.Add((addedFile, match));
                renamedAdded.Add(addedFile);
                renamedRemoved.Add(match);
            }
        }

        if (renamed.Count > 0)
        {
            added.RemoveAll(renamedAdded.Contains);
            removed.RemoveAll(renamedRemoved.Contains);
        }

        return (unchanged, added, removed, renamed);
    }
}
