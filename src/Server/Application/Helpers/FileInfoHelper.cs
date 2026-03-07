namespace K7.Server.Application.Helpers;
public static class FileInfoHelper
{
    public static List<FileInfo> GetAllFileInfosRecursively(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            throw new DirectoryNotFoundException($"Root directory not found: {rootDirectory}");
        }

        return [.. Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories).Select(filePath => new FileInfo(filePath))];
    }
}
