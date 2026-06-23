namespace K7.Server.Application.Common.Configuration;

public class PathsConfiguration
{
    public string Config { get; set; } = "";
    public string Metadatas { get; set; } = "";
    public string Logs { get; set; } = "";
    public string Transcoding { get; set; } = "";
    public string FFMpegBinaryFolder { get; set; } = "";

    /// <summary>
    /// Converts an absolute path to a relative path if it falls under the <see cref="Metadatas"/> base directory.
    /// Paths outside the metadata root are returned unchanged.
    /// </summary>
    public string ToRelativeMetadataPath(string absolutePath)
    {
        var fullMetadataPath = Path.GetFullPath(Metadatas);
        var fullAbsolutePath = Path.GetFullPath(absolutePath);

        if (fullAbsolutePath.StartsWith(fullMetadataPath, StringComparison.OrdinalIgnoreCase))
            return Path.GetRelativePath(fullMetadataPath, fullAbsolutePath);

        return absolutePath;
    }

    /// <summary>
    /// Resolves a stored path back to an absolute path.
    /// Relative paths are resolved against <see cref="Metadatas"/>; already-rooted paths are returned unchanged.
    /// </summary>
    public string ResolveMetadataPath(string storedPath)
    {
        if (Path.IsPathRooted(storedPath))
            return storedPath;

        return Path.Combine(Metadatas, storedPath);
    }
}
