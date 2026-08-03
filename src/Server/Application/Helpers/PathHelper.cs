namespace K7.Server.Application.Helpers;

public static class PathHelper
{
    /// <summary>
    /// Full parent directory of a file path. Prefer this over <c>IndexedFile.ParentDirectory</c>
    /// when grouping or matching by folder identity - that field stores only the folder name
    /// (e.g. "Season 01"), which collides across different series/album trees.
    /// </summary>
    public static string? GetContainingDirectoryPath(string? filePath) =>
        string.IsNullOrEmpty(filePath) ? null : Path.GetDirectoryName(filePath);

    public static bool ContainingDirectoriesEqual(string? leftFilePath, string? rightFilePath)
    {
        var left = GetContainingDirectoryPath(leftFilePath);
        var right = GetContainingDirectoryPath(rightFilePath);
        if (left is null || right is null)
            return false;

        return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsInContainingDirectory(string? filePath, string? directoryPath)
    {
        var containing = GetContainingDirectoryPath(filePath);
        if (containing is null || string.IsNullOrEmpty(directoryPath))
            return false;

        return string.Equals(NormalizePath(containing), NormalizePath(directoryPath), StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
    }

    public static string NormalizePath(string path, string rootPath)
    {
        if (Path.IsPathRooted(path))
            return NormalizePath(path);

        return NormalizePath(Path.Combine(rootPath, path));
    }

    public static string NormalizeLibraryPath(string path, string libraryRootPath)
    {
        if (string.IsNullOrWhiteSpace(path))
            return NormalizePath(path);

        if (Path.IsPathRooted(path))
            return NormalizePath(path);

        return NormalizePath(Path.Combine(libraryRootPath, path));
    }

    public static bool IsPathInScope(string filePath, string scopePath)
    {
        var normalizedFile = NormalizePath(filePath);
        var normalizedScope = NormalizePath(scopePath);

        if (normalizedFile.Equals(normalizedScope, StringComparison.OrdinalIgnoreCase))
            return true;

        var scopePrefix = normalizedScope.TrimEnd(Path.DirectorySeparatorChar);
        scopePrefix += Path.DirectorySeparatorChar;

        return normalizedFile.StartsWith(scopePrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPathUnderRoot(string path, string rootPath)
        => IsPathInScope(path, rootPath);
}
