using K7.Server.Domain.Entities;

namespace K7.Server.Application.Helpers;

public enum LibraryScanRemovalBlockReason
{
    None,
    LibraryRootUnavailable,
    NoFilesFound
}

public sealed record LibraryScanRemovalDecision(
    IReadOnlyList<IndexedFile> FilesToRemove,
    IReadOnlyList<IndexedFile> ProtectedFiles,
    LibraryScanRemovalBlockReason BlockReason);

/// <summary>
/// Decides which indexed files a scan may delete. Removal trusts scan membership, so a share
/// that is unmounted (empty mount point) or a folder that cannot be read would otherwise look
/// like deleted content and drop the files with their metadata and user state.
/// </summary>
public static class LibraryScanRemovalGuard
{
    public static LibraryScanRemovalDecision Apply(
        IReadOnlyList<IndexedFile> removedFiles,
        string libraryRootPath,
        bool isFullScan,
        int scannedFileCount,
        bool libraryRootAvailable,
        IReadOnlyList<(string Path, string Error)> inaccessiblePaths)
    {
        if (removedFiles.Count == 0)
            return new LibraryScanRemovalDecision([], [], LibraryScanRemovalBlockReason.None);

        if (!libraryRootAvailable)
            return new LibraryScanRemovalDecision([], removedFiles, LibraryScanRemovalBlockReason.LibraryRootUnavailable);

        if (isFullScan && scannedFileCount == 0)
            return new LibraryScanRemovalDecision([], removedFiles, LibraryScanRemovalBlockReason.NoFilesFound);

        // A path that no longer exists is a real deletion; only access failures protect files.
        var unreadablePaths = inaccessiblePaths
            .Where(p => !string.Equals(p.Error, FileInfoHelper.PathDoesNotExistError, StringComparison.Ordinal))
            .Select(p => PathHelper.NormalizePath(p.Path, libraryRootPath))
            .ToList();

        if (unreadablePaths.Count == 0)
            return new LibraryScanRemovalDecision(removedFiles, [], LibraryScanRemovalBlockReason.None);

        List<IndexedFile> toRemove = [];
        List<IndexedFile> protectedFiles = [];
        foreach (var file in removedFiles)
        {
            var filePath = PathHelper.NormalizeLibraryPath(file.Path, libraryRootPath);
            if (unreadablePaths.Any(path => PathHelper.IsPathInScope(filePath, path)))
                protectedFiles.Add(file);
            else
                toRemove.Add(file);
        }

        return new LibraryScanRemovalDecision(toRemove, protectedFiles, LibraryScanRemovalBlockReason.None);
    }
}
