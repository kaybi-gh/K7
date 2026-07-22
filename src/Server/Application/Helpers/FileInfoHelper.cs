using K7.Server.Application.Extensions;
using K7.Server.Application.Models;

namespace K7.Server.Application.Helpers;

public static class FileInfoHelper
{
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
            if (!fileInfo.Exists || !fileInfo.IsSupportedFile())
                continue;

            files.Add(fileInfo.ToScannedFileEntry());
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

            if (File.Exists(path))
            {
                try
                {
                    var fileInfo = new FileInfo(path);
                    if (fileInfo.Exists && fileInfo.IsSupportedFile())
                        files.Add(fileInfo.ToScannedFileEntry());
                }
                catch (Exception ex)
                {
                    inaccessiblePaths.Add((path, ex.Message));
                }

                continue;
            }

            if (Directory.Exists(path))
            {
                var (dirFiles, dirErrors) = GetSupportedFilesRecursively(path, cancellationToken);
                files.AddRange(dirFiles);
                inaccessiblePaths.AddRange(dirErrors);
                continue;
            }

            inaccessiblePaths.Add((path, "Path does not exist."));
        }

        var distinctFiles = files
            .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        return (distinctFiles, inaccessiblePaths);
    }

    public static (List<FileInfo> Files, List<(string Path, string Error)> InaccessiblePaths) GetAllFileInfosRecursively(string rootDirectory, CancellationToken cancellationToken = default)
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

            try
            {
                foreach (var filePath in Directory.EnumerateFiles(currentDir))
                {
                    files.Add(new FileInfo(filePath));
                }
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

            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(currentDir))
                {
                    var dirName = Path.GetFileName(subDir);

                    if (IsExcludedDirectoryName(dirName))
                        continue;

                    stack.Push(subDir);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                inaccessiblePaths.Add((currentDir, ex.Message));
            }
            catch (IOException ex)
            {
                inaccessiblePaths.Add((currentDir, ex.Message));
            }
        }

        return (files, inaccessiblePaths);
    }
}
