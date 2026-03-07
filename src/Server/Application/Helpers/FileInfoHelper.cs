using System.Collections.Concurrent;

namespace K7.Server.Application.Helpers;
public static class FileInfoHelper
{
    public static List<FileInfo> GetAllFileInfosRecursively(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            throw new DirectoryNotFoundException($"Root directory not found: {rootDirectory}");
        }

        ConcurrentBag<FileInfo> fileInfos = [];
        var allFiles = Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories);
        //.Where(f => f.LastWriteTime >= since || f.CreationTime >= since); // TODO - Only get latest?
        Parallel.ForEach(allFiles, filePath => fileInfos.Add(new FileInfo(filePath)));

        return [.. fileInfos];
    }
}
