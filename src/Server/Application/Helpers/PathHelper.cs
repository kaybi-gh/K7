namespace K7.Server.Application.Helpers;

public static class PathHelper
{
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
