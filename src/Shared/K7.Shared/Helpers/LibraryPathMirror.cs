using System.Text.Json;

namespace K7.Shared.Helpers;

public static class LibraryPathMirror
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string? TryGetRelativePath(string? filePath, string? libraryRootPath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(libraryRootPath))
            return null;

        var file = Normalize(filePath);
        var root = Normalize(libraryRootPath).TrimEnd('/');
        if (file.Length <= root.Length)
            return null;

        var comparison = file.StartsWith('/') && root.StartsWith('/')
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        if (!file.StartsWith(root, comparison) || file[root.Length] != '/')
            return null;

        var relative = file[(root.Length + 1)..];
        if (string.IsNullOrEmpty(relative) || HasTraversal(relative))
            return null;

        return relative;
    }

    public static string? TryCombine(string? localRoot, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(localRoot) || string.IsNullOrWhiteSpace(relativePath))
            return null;

        if (HasTraversal(relativePath))
            return null;

        var combined = localRoot.Trim().TrimEnd('\\', '/');
        foreach (var part in relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part is "." or "..")
                return null;

            combined = Path.Combine(combined, part);
        }

        return combined;
    }

    public static string Serialize(IReadOnlyDictionary<Guid, string> map)
    {
        var trimmed = map
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Trim());

        if (trimmed.Count == 0)
            return "";

        return JsonSerializer.Serialize(trimmed, JsonOptions);
    }

    public static Dictionary<Guid, string> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<Guid, string>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<Guid, string>>(json, JsonOptions)
                   ?? new Dictionary<Guid, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<Guid, string>();
        }
    }

    public static bool HasTraversal(string relativePath) =>
        relativePath.Replace('\\', '/').Split('/').Any(part => part is "..");

    private static string Normalize(string path) =>
        path.Replace('\\', '/').TrimEnd('/');
}
