using K7.Server.Application.Common.Configuration;

namespace K7.Server.Application.Helpers;

public static class ThemeSongLocator
{
    private static readonly string[] ThemeFileNames =
    [
        "theme.mp3",
        "theme.flac",
        "theme.m4a",
        "theme.ogg"
    ];

    private static readonly string[] AudioExtensions =
    [
        ".mp3",
        ".flac",
        ".m4a",
        ".ogg"
    ];

    public static string GetGeneratedPath(PathsConfiguration paths, Guid mediaId) =>
        Path.Combine(paths.Metadatas, "medias", mediaId.ToString(), "theme.mp3");

    /// <summary>
    /// Serie root is typically parent of the season folder that contains the episode file.
    /// </summary>
    public static string? ResolveSerieRootFromEpisodePath(string episodeFilePath)
    {
        var seasonDirectory = Path.GetDirectoryName(episodeFilePath);
        if (string.IsNullOrEmpty(seasonDirectory))
            return null;

        return Path.GetDirectoryName(seasonDirectory);
    }

    public static string? FindLibrarySidecar(string? folderPath, string? videoFilePath = null)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            return null;

        foreach (var fileName in ThemeFileNames)
        {
            var candidate = Path.Combine(folderPath, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        if (string.IsNullOrEmpty(videoFilePath))
            return null;

        var stem = Path.GetFileNameWithoutExtension(videoFilePath);
        if (string.IsNullOrEmpty(stem))
            return null;

        foreach (var extension in AudioExtensions)
        {
            var candidate = Path.Combine(folderPath, stem + extension);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
