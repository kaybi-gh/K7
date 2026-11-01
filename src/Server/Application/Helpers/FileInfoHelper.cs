using K7.Server.Application.Extensions;
using K7.Server.Application.Models;

namespace K7.Server.Application.Helpers;

public static class FileInfoHelper
{
    /// <summary>
    /// Error reported for a scoped scan path that is gone. Unlike access errors it means the
    /// content was really removed, so the scan may drop its indexed files.
    /// </summary>
    public const string PathDoesNotExistError = "Path does not exist.";

    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "@eaDir",
        ".synology",
        "#recycle",
        "@Recycle",
        ".@__thumb",
        "@tmp",
        ".DS_Store"
    };

    public static bool IsExcludedDirectoryName(string? directoryName)
    {
        if (string.IsNullOrEmpty(directoryName))
            return false;

        return ExcludedDirectoryNames.Contains(directoryName)
            || directoryName.StartsWith(".Trash-", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsExcludedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var segments = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(IsExcludedDirectoryName);
    }

    public static (List<ScannedFileEntry> Files, List<(string Path, string Error)> InaccessiblePaths) GetSupportedFilesRecursively(
        string rootDirectory,
        CancellationToken cancellationToken = default)
    {
        var (fileInfos, inaccessiblePaths) = GetAllFileInfosRecursively(rootDirectory, cancellationToken);
        var files = new List<ScannedFileEntry>(fileInfos.Count);

        foreach (var fileInfo in fileInfos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!fileInfo.IsSupportedFile())
                continue;

            try
            {
                var entry = FileSystemIo.Run(
                    fileInfo.ToScannedFileEntry,
                    FileSystemIo.FileAccessTimeout,
                    cancellationToken);
                files.Add(entry);
            }
            catch (TimeoutException)
            {
                inaccessiblePaths.Add((fileInfo.FullName, $"Timed out after {FileSystemIo.FileAccessTimeout.TotalSeconds:0}s reading file metadata."));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                inaccessiblePaths.Add((fileInfo.FullName, ex.Message));
            }
        }

        return (files, inaccessiblePaths);
    }

    public static (List<ScannedFileEntry> Files, List<(string Path, string Error)> InaccessiblePaths) GetSupportedFilesForPaths(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken = default)
    {
        var files = new List<ScannedFileEntry>();
        var inaccessiblePaths = new List<(string Path, string Error)>();

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsExcludedPath(path))
                continue;

            bool isFile;
            bool isDirectory;
            try
            {
                (isFile, isDirectory) = FileSystemIo.Run(
                    () => (File.Exists(path), Directory.Exists(path)),
                    FileSystemIo.FileAccessTimeout,
                    cancellationToken);
            }
            catch (TimeoutException)
            {
                inaccessiblePaths.Add((path, $"Timed out after {FileSystemIo.FileAccessTimeout.TotalSeconds:0}s checking path."));
                continue;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                inaccessiblePaths.Add((path, ex.Message));
                continue;
            }

            if (isFile)
            {
                try
                {
                    var fileInfo = new FileInfo(path);
                    if (fileInfo.IsSupportedFile())
                    {
                        var entry = FileSystemIo.Run(
                            fileInfo.ToScannedFileEntry,
                            FileSystemIo.FileAccessTimeout,
                            cancellationToken);
                        files.Add(entry);
                    }
                }
                catch (TimeoutException)
                {
                    inaccessiblePaths.Add((path, $"Timed out after {FileSystemIo.FileAccessTimeout.TotalSeconds:0}s reading file metadata."));
                }
                catch (Exception ex)
                {
                    inaccessiblePaths.Add((path, ex.Message));
                }

                continue;
            }

            if (isDirectory)
            {
                var (dirFiles, dirErrors) = GetSupportedFilesRecursively(path, cancellationToken);
                files.AddRange(dirFiles);
                inaccessiblePaths.AddRange(dirErrors);
                continue;
            }

            inaccessiblePaths.Add((path, PathDoesNotExistError));
        }

        var distinctFiles = files
            .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        return (distinctFiles, inaccessiblePaths);
    }

    /// <summary>
    /// True when the directory exists and lists at least one entry. An unmounted network share
    /// usually leaves its mount point empty; a timeout or an access error counts as unavailable.
    /// </summary>
    public static bool IsDirectoryPopulated(string directoryPath, CancellationToken cancellationToken = default)
    {
        try
        {
            return FileSystemIo.Run(
                () => Directory.Exists(directoryPath) && Directory.EnumerateFileSystemEntries(directoryPath).Any(),
                FileSystemIo.FileAccessTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static (List<FileInfo> Files, List<(string Path, string Error)> InaccessiblePaths) GetAllFileInfosRecursively(
        string rootDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(rootDirectory))
        {
            throw new DirectoryNotFoundException($"Root directory not found: {rootDirectory}");
        }

        var rootName = Path.GetFileName(rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (IsExcludedDirectoryName(rootName))
        {
            return ([], []);
        }

        var files = new List<FileInfo>();
        var inaccessiblePaths = new List<(string Path, string Error)>();
        var stack = new Stack<string>();
        stack.Push(rootDirectory);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDir = stack.Pop();

            List<string> filePaths;
            try
            {
                filePaths = FileSystemIo.Run(
                    () => Directory.EnumerateFiles(currentDir).ToList(),
                    FileSystemIo.DirectoryEnumerationTimeout,
                    cancellationToken);
            }
            catch (TimeoutException)
            {
                inaccessiblePaths.Add((
                    currentDir,
                    $"Timed out after {FileSystemIo.DirectoryEnumerationTimeout.TotalSeconds:0}s enumerating files."));
                continue;
            }
            catch (UnauthorizedAccessException ex)
            {
                inaccessiblePaths.Add((currentDir, ex.Message));
                continue;
            }
            catch (IOException ex)
            {
                inaccessiblePaths.Add((currentDir, ex.Message));
                continue;
            }

            foreach (var filePath in filePaths)
                files.Add(new FileInfo(filePath));

            List<string> subDirs;
            try
            {
                subDirs = FileSystemIo.Run(
                    () => Directory.EnumerateDirectories(currentDir).ToList(),
                    FileSystemIo.DirectoryEnumerationTimeout,
                    cancellationToken);
            }
            catch (TimeoutException)
            {
                inaccessiblePaths.Add((
                    currentDir,
                    $"Timed out after {FileSystemIo.DirectoryEnumerationTimeout.TotalSeconds:0}s enumerating subdirectories."));
                continue;
            }
            catch (UnauthorizedAccessException ex)
            {
                inaccessiblePaths.Add((currentDir, ex.Message));
                continue;
            }
            catch (IOException ex)
            {
                inaccessiblePaths.Add((currentDir, ex.Message));
                continue;
            }

            foreach (var subDir in subDirs)
            {
                var dirName = Path.GetFileName(subDir);
                if (IsExcludedDirectoryName(dirName))
                    continue;

                stack.Push(subDir);
            }
        }

        return (files, inaccessiblePaths);
    }
}
