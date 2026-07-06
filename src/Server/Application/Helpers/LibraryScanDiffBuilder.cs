using K7.Server.Application.Models;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.Helpers;

public sealed class LibraryScanDiff
{
    public required List<IndexedFile> UnchangedFiles { get; init; }
    public required List<IndexedFile> AddedFiles { get; init; }
    public required List<IndexedFile> RemovedFiles { get; init; }
    public required List<(IndexedFile NewFile, IndexedFile OldFile)> RenamedFiles { get; init; }
    public required List<(IndexedFile ExistingFile, ScannedFileEntry ScannedFile)> ModifiedFiles { get; init; }
    public required List<(IndexedFile ExistingFile, ScannedFileEntry ScannedFile)> BackfillTimestampFiles { get; init; }
}

public static class LibraryScanDiffBuilder
{
    public static LibraryScanDiff Build(
        IReadOnlyList<ScannedFileEntry> scannedFiles,
        IReadOnlyList<IndexedFile> existingFiles,
        HashSet<string> skippedFilePaths,
        Func<ScannedFileEntry, IndexedFile> createIndexedFile)
    {
        var scanned = DedupeScannedFiles(scannedFiles);
        var (dedupedExisting, duplicateExisting) = DedupeExistingFiles(existingFiles);
        var existingByPath = dedupedExisting.ToDictionary(
            f => PathHelper.NormalizePath(f.Path),
            f => f,
            StringComparer.OrdinalIgnoreCase);
        var matchedExisting = new HashSet<IndexedFile>();
        List<IndexedFile> unchanged = [];
        List<IndexedFile> addedCandidates = [];
        List<(IndexedFile ExistingFile, ScannedFileEntry ScannedFile)> modified = [];
        List<(IndexedFile ExistingFile, ScannedFileEntry ScannedFile)> backfillTimestamp = [];

        foreach (var scannedFile in scanned)
        {
            var normalizedScannedPath = PathHelper.NormalizePath(scannedFile.Path);
            if (existingByPath.TryGetValue(normalizedScannedPath, out var existing))
            {
                matchedExisting.Add(existing);

                if (FileTimestampHelper.HasSameContent(existing.LastWriteTimeUtc, existing.Size, scannedFile.LastWriteTimeUtc, scannedFile.Size))
                {
                    if (FileTimestampHelper.NeedsLastWriteTimeBackfill(existing.LastWriteTimeUtc))
                        backfillTimestamp.Add((existing, scannedFile));
                    else
                        unchanged.Add(existing);
                }
                else
                {
                    modified.Add((existing, scannedFile));
                }
            }
            else
            {
                addedCandidates.Add(createIndexedFile(scannedFile));
            }
        }

        var removed = dedupedExisting
            .Where(f => !matchedExisting.Contains(f))
            .Where(f => !skippedFilePaths.Contains(f.Path))
            .Concat(duplicateExisting)
            .Distinct()
            .ToList();

        var (renamed, added, renamedRemoved) = MatchRenames(addedCandidates, removed);

        removed.RemoveAll(renamedRemoved.Contains);

        return new LibraryScanDiff
        {
            UnchangedFiles = unchanged,
            AddedFiles = added,
            RemovedFiles = removed,
            RenamedFiles = renamed,
            ModifiedFiles = modified,
            BackfillTimestampFiles = backfillTimestamp
        };
    }

    private static List<ScannedFileEntry> DedupeScannedFiles(IReadOnlyList<ScannedFileEntry> scannedFiles) =>
        scannedFiles
            .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

    private static (List<IndexedFile> Canonical, List<IndexedFile> Duplicates) DedupeExistingFiles(IReadOnlyList<IndexedFile> existingFiles)
    {
        List<IndexedFile> canonical = [];
        List<IndexedFile> duplicates = [];

        foreach (var group in existingFiles.GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderByDescending(f => f.Created).ToList();
            canonical.Add(ordered[0]);
            duplicates.AddRange(ordered.Skip(1));
        }

        return (canonical, duplicates);
    }

    private static (List<(IndexedFile NewFile, IndexedFile OldFile)> Renamed, List<IndexedFile> Added, HashSet<IndexedFile> RenamedRemoved) MatchRenames(
        List<IndexedFile> addedCandidates,
        List<IndexedFile> removed)
    {
        List<(IndexedFile NewFile, IndexedFile OldFile)> renamed = [];
        var renamedRemoved = new HashSet<IndexedFile>();
        var renamedAdded = new HashSet<IndexedFile>();
        var remainingAdded = new List<IndexedFile>(addedCandidates);

        var removedByParentAndSize = removed
            .Where(f => f.ParentDirectory is not null)
            .ToLookup(f => (f.ParentDirectory!, f.Size));

        foreach (var addedFile in addedCandidates)
        {
            if (addedFile.ParentDirectory is null)
                continue;

            var candidates = removedByParentAndSize[(addedFile.ParentDirectory, addedFile.Size)]
                .Where(c => !renamedRemoved.Contains(c))
                .ToList();

            if (candidates.Count != 1)
                continue;

            var match = candidates[0];
            renamed.Add((addedFile, match));
            renamedAdded.Add(addedFile);
            renamedRemoved.Add(match);
        }

        remainingAdded.RemoveAll(renamedAdded.Contains);

        var removedByHashSize = removed
            .Where(f => !renamedRemoved.Contains(f))
            .ToLookup(f => (f.Hash, f.Size));

        foreach (var addedFile in remainingAdded.ToList())
        {
            var candidates = removedByHashSize[(addedFile.Hash, addedFile.Size)]
                .Where(c => !renamedRemoved.Contains(c))
                .ToList();

            if (candidates.Count != 1)
                continue;

            var match = candidates[0];
            renamed.Add((addedFile, match));
            renamedAdded.Add(addedFile);
            renamedRemoved.Add(match);
        }

        remainingAdded.RemoveAll(renamedAdded.Contains);
        return (renamed, remainingAdded, renamedRemoved);
    }
}
