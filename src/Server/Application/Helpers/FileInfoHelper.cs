namespace K7.Server.Application.Helpers;
public static class FileInfoHelper
{
    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "@eaDir",
        ".synology",
        "#recycle",
        "@Recycle",
        ".DS_Store"
    };

    public static (List<FileInfo> Files, List<(string Path, string Error)> InaccessiblePaths) GetAllFileInfosRecursively(string rootDirectory, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(rootDirectory))
        {
            throw new DirectoryNotFoundException($"Root directory not found: {rootDirectory}");
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

                    if (ExcludedDirectoryNames.Contains(dirName)
                        || dirName.StartsWith(".Trash-", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

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
